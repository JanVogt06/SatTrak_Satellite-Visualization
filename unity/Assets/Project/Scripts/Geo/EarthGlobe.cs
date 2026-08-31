using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Geo
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class EarthGlobe : MonoBehaviour
    {
        [SerializeField] private Georeference _georeference;

        [Header("Tessellation")]
        [Range(16, 1024)] [SerializeField] private int _longitudeSegments = 256;
        [Range(8, 512)] [SerializeField] private int _latitudeSegments = 128;

        private Mesh _mesh;
        private double4x4 _builtWithMatrix;
        private int _builtLongitudeSegments;
        private int _builtLatitudeSegments;
        private bool _built;

        private Georeference Georeference
        {
            get
            {
                if (_georeference == null) _georeference = GetComponentInParent<Georeference>();
                return _georeference;
            }
        }

        private void OnEnable() => _built = false;

        private void OnValidate() => _built = false;

        private void Update()
        {
            var geo = Georeference;
            if (geo == null) return;

            if (_built
                && _builtLongitudeSegments == _longitudeSegments
                && _builtLatitudeSegments == _latitudeSegments
                && _builtWithMatrix.Equals(geo.ecefToLocalMatrix))
                return;

            Rebuild();
        }

        private void Rebuild()
        {
            var geo = Georeference;
            if (geo == null) return;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Earth", indexFormat = IndexFormat.UInt32 };
                _mesh.hideFlags = HideFlags.DontSave;
            }

            int columns = _longitudeSegments + 1;
            int rows = _latitudeSegments + 1;

            var vertices = new Vector3[columns * rows];
            var normals = new Vector3[columns * rows];
            var uvs = new Vector2[columns * rows];

            for (int row = 0; row < rows; row++)
            {
                double v = row / (double)_latitudeSegments;
                double lat = -90.0 + v * 180.0;

                for (int column = 0; column < columns; column++)
                {
                    double u = column / (double)_longitudeSegments;
                    double lon = -180.0 + u * 360.0;

                    var ecef = Wgs84.LongitudeLatitudeHeightToEcef(new double3(lon, lat, 0.0));
                    var local = geo.TransformEarthCenteredEarthFixedPositionToUnity(ecef);

                    var upEcef = math.normalize(new double3(
                        ecef.x / (Wgs84.SemiMajorAxis * Wgs84.SemiMajorAxis),
                        ecef.y / (Wgs84.SemiMajorAxis * Wgs84.SemiMajorAxis),
                        ecef.z / (Wgs84.SemiMinorAxis * Wgs84.SemiMinorAxis)));
                    var upLocal = geo.TransformEarthCenteredEarthFixedDirectionToUnity(upEcef);

                    int index = row * columns + column;
                    vertices[index] = new Vector3((float)local.x, (float)local.y, (float)local.z);
                    normals[index] = math.normalize(new Vector3((float)upLocal.x, (float)upLocal.y, (float)upLocal.z));
                    uvs[index] = new Vector2((float)u, (float)v);
                }
            }

            bool flip = NeedsFlippedWinding(vertices, normals, columns, rows);
            var triangles = new int[_longitudeSegments * _latitudeSegments * 6];
            int cursor = 0;

            for (int row = 0; row < _latitudeSegments; row++)
            {
                for (int column = 0; column < _longitudeSegments; column++)
                {
                    int a = row * columns + column;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;

                    if (flip)
                    {
                        triangles[cursor++] = a; triangles[cursor++] = c; triangles[cursor++] = b;
                        triangles[cursor++] = b; triangles[cursor++] = c; triangles[cursor++] = d;
                    }
                    else
                    {
                        triangles[cursor++] = a; triangles[cursor++] = b; triangles[cursor++] = c;
                        triangles[cursor++] = b; triangles[cursor++] = d; triangles[cursor++] = c;
                    }
                }
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            _builtWithMatrix = geo.ecefToLocalMatrix;
            _builtLongitudeSegments = _longitudeSegments;
            _builtLatitudeSegments = _latitudeSegments;
            _built = true;
        }

        private static bool NeedsFlippedWinding(Vector3[] vertices, Vector3[] normals, int columns, int rows)
        {
            int sample = (rows / 2) * columns + columns / 2;

            var a = vertices[sample];
            var b = vertices[sample + 1];
            var c = vertices[sample + columns];

            var faceNormal = Vector3.Cross(b - a, c - a);
            return Vector3.Dot(faceNormal, normals[sample]) < 0f;
        }
    }
}
