using Unity.Mathematics;
using UnityEngine;

namespace Geo
{
    [ExecuteAlways]
    public class GlobeAnchor : MonoBehaviour
    {
        [SerializeField] private Georeference _georeference;

        private double3 _ecef;
        private Vector3 _lastAppliedPosition;
        private bool _hasEcef;

        public Georeference georeference
        {
            get
            {
                if (_georeference == null) _georeference = GetComponentInParent<Georeference>();
                return _georeference;
            }
            set => _georeference = value;
        }

        public double3 positionEarthCenteredEarthFixed
        {
            get { SyncFromTransform(); return _ecef; }
            set
            {
                _ecef = value;
                _hasEcef = true;
                ApplyToTransform();
            }
        }

        public double3 longitudeLatitudeHeight
        {
            get => Wgs84.EcefToLongitudeLatitudeHeight(positionEarthCenteredEarthFixed);
            set => positionEarthCenteredEarthFixed = Wgs84.LongitudeLatitudeHeightToEcef(value);
        }

        private void OnEnable()
        {
            var geo = georeference;
            if (geo == null) return;

            geo.Register(this);
            SyncFromTransform();
        }

        private void OnDisable()
        {
            if (_georeference != null) _georeference.Unregister(this);
        }

        public void SyncFromTransform()
        {
            var geo = georeference;
            if (geo == null) return;

            if (_hasEcef && transform.position == _lastAppliedPosition) return;

            _ecef = geo.TransformUnityPositionToEarthCenteredEarthFixed(ToDouble3(transform.position));
            _lastAppliedPosition = transform.position;
            _hasEcef = true;
        }

        public void ApplyToTransform()
        {
            var geo = georeference;
            if (geo == null || !_hasEcef) return;

            var local = geo.TransformEarthCenteredEarthFixedPositionToUnity(_ecef);
            var position = new Vector3((float)local.x, (float)local.y, (float)local.z);

            transform.position = position;
            _lastAppliedPosition = position;
        }

        private static double3 ToDouble3(Vector3 v) => new(v.x, v.y, v.z);
    }
}
