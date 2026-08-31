using System;
using System.Collections;
using System.Collections.Generic;
using Satellites.SGP.TLE;
using UnityEngine;
using UnityEngine.Networking;

namespace Satellites
{
    public class TleSource
    {
        private const string UserAgent = "SatTrak (https://github.com/JanVogt06/SatTrak-SatelliteVisualization)";

        private readonly string _liveUrl;
        private readonly string _snapshotUrl;

        public TleSource(string liveUrl, string snapshotFileName)
        {
            _liveUrl = string.IsNullOrWhiteSpace(liveUrl) ? null : liveUrl.Trim();
            _snapshotUrl = BuildStreamingAssetsUrl(snapshotFileName);
        }

        public IEnumerator Load(Action<Dictionary<int, Tle>> onLoaded, Action<string> onFailed)
        {
            var failures = new List<string>();

            foreach (var url in new[] { _liveUrl, _snapshotUrl })
            {
                if (url == null) continue;

                Dictionary<int, Tle> tles = null;
                string failure = null;

                yield return Fetch(url, result => tles = result, message => failure = message);

                if (tles != null)
                {
                    Debug.Log($"[TleSource] Loaded {tles.Count} TLE records from {url}");
                    onLoaded(tles);
                    yield break;
                }

                failures.Add($"{url} -> {failure}");
            }

            onFailed(string.Join(" | ", failures));
        }

        private static IEnumerator Fetch(string url, Action<Dictionary<int, Tle>> onParsed, Action<string> onFailed)
        {
            string body = null;
            string transportError = null;

            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("User-Agent", UserAgent);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                    body = request.downloadHandler.text;
                else
                    transportError = $"{request.result}: {request.error}";
            }

            if (transportError != null)
            {
                onFailed(transportError);
                yield break;
            }

            if (!TryParse(body, out var tles))
            {
                onFailed($"not TLE data: \"{Summarize(body)}\"");
                yield break;
            }

            onParsed(tles);
        }

        private static string BuildStreamingAssetsUrl(string fileName)
        {
            string basePath = Application.streamingAssetsPath;
            string path = basePath.EndsWith("/") ? basePath + fileName : basePath + "/" + fileName;
            return path.Contains("://") ? path : "file://" + path;
        }

        private static bool TryParse(string body, out Dictionary<int, Tle> tles)
        {
            tles = null;
            if (string.IsNullOrEmpty(body)) return false;

            var lines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            int complete = (lines.Length / 3) * 3;
            if (complete == 0) return false;

            for (int i = 0; i < complete; i += 3)
            {
                if (!lines[i + 1].StartsWith("1 ") || !lines[i + 2].StartsWith("2 "))
                    return false;
            }

            if (complete != lines.Length)
                Array.Resize(ref lines, complete);

            var parsed = new Dictionary<int, Tle>();
            try
            {
                foreach (var tle in Tle.ParseElements(lines, true))
                    parsed[(int)tle.NoradNumber] = tle;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TleSource] TLE data could not be parsed: {e.Message}");
                return false;
            }

            if (parsed.Count == 0) return false;

            tles = parsed;
            return true;
        }

        private static string Summarize(string body)
        {
            if (string.IsNullOrEmpty(body)) return "empty response";
            var text = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 160 ? text : text.Substring(0, 160) + "...";
        }
    }
}
