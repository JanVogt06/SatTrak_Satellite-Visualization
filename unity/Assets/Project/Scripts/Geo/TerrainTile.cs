using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Geo
{
    public class TerrainTile
    {
        public enum State
        {
            Loading,
            Ready,
            Failed
        }

        public readonly TerrainTileKey Key;
        public State Status = State.Loading;
        public float LastUsed;

        private readonly int _grid;
        private float[] _heights;
        private Texture2D _imagery;
        private GameObject _gameObject;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _sink = -1f;
        private Mesh _mesh;
        private double4x4 _builtWith;

        public TerrainTile(TerrainTileKey key, int grid)
        {
            Key = key;
            _grid = grid;
        }

        public bool Visible
        {
            set { if (_gameObject != null && _gameObject.activeSelf != value) _gameObject.SetActive(value); }
        }

        public float Sink
        {
            set
            {
                if (_renderer == null || Mathf.Approximately(_sink, value)) return;
                _sink = value;
                _block.SetFloat("_Sink", value);
                _renderer.SetPropertyBlock(_block);
            }
        }

        public void SetElevation(Texture2D terrarium)
        {
            int size = _grid + 1;
            _heights = new float[size * size];

            if (terrarium == null) return;

            var pixels = terrarium.GetPixels32();
            int width = terrarium.width;
            int height = terrarium.height;

            for (int j = 0; j < size; j++)
            {
                float py = j / (float)_grid * (height - 1);
                for (int i = 0; i < size; i++)
                {
                    float px = i / (float)_grid * (width - 1);
                    _heights[j * size + i] = Mathf.Max(0f, SampleTerrarium(pixels, width, height, px, py));
                }
            }
        }

        public void SetImagery(Texture2D imagery)
        {
            _imagery = imagery;
            _imagery.wrapMode = TextureWrapMode.Clamp;
            _imagery.anisoLevel = 4;
        }

        public void Build(Transform parent, Material material, Georeference georeference, float skirtDepth)
        {
            if (_gameObject == null)
            {
                _gameObject = new GameObject($"Terrain {Key}");
                _gameObject.transform.SetParent(parent, false);
                _gameObject.AddComponent<MeshFilter>();
                _renderer = _gameObject.AddComponent<MeshRenderer>();
                _renderer.sharedMaterial = material;
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;

                _block = new MaterialPropertyBlock();
                _block.SetTexture("_MainTex", _imagery);
                _block.SetFloat("_Sink", 0f);
                _renderer.SetPropertyBlock(_block);
                _sink = 0f;

                _mesh = new Mesh { name = $"Terrain {Key}", indexFormat = IndexFormat.UInt16 };
                _gameObject.GetComponent<MeshFilter>().sharedMesh = _mesh;
            }

            Rebuild(georeference, skirtDepth);
        }

        public void RebuildIfMoved(Georeference georeference, float skirtDepth)
        {
            if (_mesh != null && !_builtWith.Equals(georeference.ecefToLocalMatrix))
                Rebuild(georeference, skirtDepth);
        }

        private void Rebuild(Georeference georeference, float skirtDepth)
        {
            int size = _grid + 1;
            int surface = size * size;
            int skirt = 4 * _grid;

            var vertices = new Vector3[surface + skirt];
            var normals = new Vector3[surface + skirt];
            var uvs = new Vector2[surface + skirt];

            var centerLonLat = Key.LongitudeLatitude(0.5, 0.5);
            var origin = georeference.TransformEarthCenteredEarthFixedPositionToUnity(
                Wgs84.LongitudeLatitudeHeightToEcef(new double3(centerLonLat, 0.0)));

            for (int j = 0; j < size; j++)
            {
                double v = j / (double)_grid;
                for (int i = 0; i < size; i++)
                {
                    double u = i / (double)_grid;
                    int index = j * size + i;
                    vertices[index] = ToLocal(georeference, Key.LongitudeLatitude(u, v), _heights[index], origin);
                    uvs[index] = new Vector2((float)u, (float)(1.0 - v));
                }
            }

            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    var left = vertices[j * size + math.max(i - 1, 0)];
                    var right = vertices[j * size + math.min(i + 1, _grid)];
                    var up = vertices[math.max(j - 1, 0) * size + i];
                    var down = vertices[math.min(j + 1, _grid) * size + i];
                    normals[j * size + i] = Vector3.Cross(up - down, right - left).normalized;
                }
            }

            var centerEcef = Wgs84.LongitudeLatitudeHeightToEcef(new double3(centerLonLat, 0.0));
            var outward = georeference.TransformEarthCenteredEarthFixedDirectionToUnity(math.normalize(centerEcef));
            if (Vector3.Dot(normals[(size / 2) * size + size / 2], new Vector3((float)outward.x, (float)outward.y, (float)outward.z)) < 0f)
            {
                for (int n = 0; n < surface; n++)
                    normals[n] = -normals[n];
            }

            var triangles = new int[_grid * _grid * 6 + skirt * 6];
            int cursor = 0;

            for (int j = 0; j < _grid; j++)
            {
                for (int i = 0; i < _grid; i++)
                {
                    int a = j * size + i;
                    int b = a + 1;
                    int c = a + size;
                    int d = c + 1;
                    triangles[cursor++] = a; triangles[cursor++] = b; triangles[cursor++] = c;
                    triangles[cursor++] = b; triangles[cursor++] = d; triangles[cursor++] = c;
                }
            }

            int ring = surface;
            foreach (int edgeIndex in EdgeLoop(size))
            {
                int next = ring + 1 == surface + skirt ? surface : ring + 1;
                int edgeNext = NextOnLoop(edgeIndex, size);

                var down = -normals[edgeIndex] * skirtDepth;
                vertices[ring] = vertices[edgeIndex] + down;
                normals[ring] = normals[edgeIndex];
                uvs[ring] = uvs[edgeIndex];

                triangles[cursor++] = edgeIndex; triangles[cursor++] = ring; triangles[cursor++] = edgeNext;
                triangles[cursor++] = edgeNext; triangles[cursor++] = ring; triangles[cursor++] = next;
                ring++;
            }

            bool flip = NeedsFlippedWinding(vertices, normals, size);
            if (flip)
            {
                for (int t = 0; t < triangles.Length; t += 3)
                    (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();

            _gameObject.transform.localPosition = new Vector3((float)origin.x, (float)origin.y, (float)origin.z);
            _builtWith = georeference.ecefToLocalMatrix;
        }

        private static Vector3 ToLocal(Georeference georeference, double2 lonLat, float height, double3 origin)
        {
            var ecef = Wgs84.LongitudeLatitudeHeightToEcef(new double3(lonLat, height));
            var local = georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef) - origin;
            return new Vector3((float)local.x, (float)local.y, (float)local.z);
        }

        private static System.Collections.Generic.IEnumerable<int> EdgeLoop(int size)
        {
            int last = size - 1;
            for (int i = 0; i < last; i++) yield return i;
            for (int j = 0; j < last; j++) yield return j * size + last;
            for (int i = last; i > 0; i--) yield return last * size + i;
            for (int j = last; j > 0; j--) yield return j * size;
        }

        private static int NextOnLoop(int index, int size)
        {
            int last = size - 1;
            int i = index % size;
            int j = index / size;
            if (j == 0 && i < last) return index + 1;
            if (i == last && j < last) return index + size;
            if (j == last && i > 0) return index - 1;
            return index - size;
        }

        private static bool NeedsFlippedWinding(Vector3[] vertices, Vector3[] normals, int size)
        {
            int sample = (size / 2) * size + size / 2;
            var faceNormal = Vector3.Cross(vertices[sample + 1] - vertices[sample], vertices[sample + size] - vertices[sample]);
            return Vector3.Dot(faceNormal, normals[sample]) < 0f;
        }

        private static float SampleTerrarium(Color32[] pixels, int width, int height, float px, float pyFromTop)
        {
            int x0 = (int)px;
            int y0 = (int)pyFromTop;
            int x1 = math.min(x0 + 1, width - 1);
            int y1 = math.min(y0 + 1, height - 1);
            float fx = px - x0;
            float fy = pyFromTop - y0;

            float h00 = Decode(pixels[(height - 1 - y0) * width + x0]);
            float h10 = Decode(pixels[(height - 1 - y0) * width + x1]);
            float h01 = Decode(pixels[(height - 1 - y1) * width + x0]);
            float h11 = Decode(pixels[(height - 1 - y1) * width + x1]);

            return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fy);
        }

        private static float Decode(Color32 c) => c.r * 256f + c.g + c.b / 256f - 32768f;

        public void Destroy()
        {
            if (_gameObject != null) Object.Destroy(_gameObject);
            if (_mesh != null) Object.Destroy(_mesh);
            if (_imagery != null) Object.Destroy(_imagery);
        }
    }
}
