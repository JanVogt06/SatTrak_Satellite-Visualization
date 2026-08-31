using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Satellites.SGP.TLE;
using UnityEngine;
using UnityEngine.Networking;

namespace Satellites
{
    public class TleSource
    {
        private const string HeaderFormat = "u";
        private const string UserAgent = "SatTrak (https://github.com/JanVogt06/SatTrak-SatelliteVisualization)";

        private readonly string _url;
        private readonly TimeSpan _maxAge;
        private readonly string _cachePath;

        public TleSource(string url, TimeSpan maxAge, string cacheFileName)
        {
            _url = url;
            _maxAge = maxAge;
            _cachePath = Path.Combine(Application.persistentDataPath, cacheFileName);
        }

        public IEnumerator Load(Action<Dictionary<int, Tle>> onLoaded, Action<string> onFailed)
        {
            if (TryReadCache(_maxAge, out var cached))
            {
                onLoaded(cached);
                yield break;
            }

            string body = null;
            string error = null;

            using (var request = UnityWebRequest.Get(_url))
            {
                request.SetRequestHeader("User-Agent", UserAgent);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                    body = request.downloadHandler.text;
                else
                    error = $"{request.result}: {request.error}";
            }

            if (body != null)
            {
                if (TryParse(body, out var downloaded))
                {
                    WriteCache(body);
                    onLoaded(downloaded);
                    yield break;
                }

                error = $"response was not TLE data: \"{Summarize(body)}\"";
            }

            if (TryReadCache(TimeSpan.MaxValue, out var stale))
            {
                Debug.LogWarning($"[TleSource] Falling back to expired cache after failed download ({error})");
                onLoaded(stale);
                yield break;
            }

            onFailed(error ?? "response could not be parsed");
        }

        private bool TryReadCache(TimeSpan maxAge, out Dictionary<int, Tle> tles)
        {
            tles = null;

            string content;
            try
            {
                if (!File.Exists(_cachePath)) return false;
                content = File.ReadAllText(_cachePath, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TleSource] Cache could not be read: {e.Message}");
                return false;
            }

            var breakIndex = content.IndexOf('\n');
            if (breakIndex < 0) return false;

            if (!DateTime.TryParseExact(content.Substring(0, breakIndex).Trim(), HeaderFormat,
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                        out var written))
                return false;

            if (DateTime.UtcNow - written > maxAge) return false;

            return TryParse(content.Substring(breakIndex + 1), out tles);
        }

        private void WriteCache(string body)
        {
            try
            {
                var content = DateTime.UtcNow.ToString(HeaderFormat, CultureInfo.InvariantCulture) + "\n" + body;
                File.WriteAllText(_cachePath, content, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TleSource] Cache could not be written: {e.Message}");
            }
        }

        private static bool TryParse(string body, out Dictionary<int, Tle> tles)
        {
            tles = null;

            var lines = body
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

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
            var text = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 160 ? text : text.Substring(0, 160) + "...";
        }
    }
}
