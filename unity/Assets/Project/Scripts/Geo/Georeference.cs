using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Geo
{
    public enum GeoreferenceOriginAuthority
    {
        EarthCenteredEarthFixed,
        LongitudeLatitudeHeight
    }

    [ExecuteAlways]
    public class Georeference : MonoBehaviour
    {
        [SerializeField] private GeoreferenceOriginAuthority _originAuthority =
            GeoreferenceOriginAuthority.LongitudeLatitudeHeight;

        [SerializeField] private double _latitude = 51.21796;
        [SerializeField] private double _longitude = 11.66699;
        [SerializeField] private double _height = 400.0;

        [SerializeField] private double _ecefX;
        [SerializeField] private double _ecefY;
        [SerializeField] private double _ecefZ;

        [SerializeField] private double _scale = 1.0;

        private readonly List<GlobeAnchor> _anchors = new();

        private double4x4 _localToEcef = double4x4.identity;
        private double4x4 _ecefToLocal = double4x4.identity;
        private bool _initialized;

        public double latitude
        {
            get => _latitude;
            set { _latitude = value; MoveOrigin(GeoreferenceOriginAuthority.LongitudeLatitudeHeight); }
        }

        public double longitude
        {
            get => _longitude;
            set { _longitude = value; MoveOrigin(GeoreferenceOriginAuthority.LongitudeLatitudeHeight); }
        }

        public double height
        {
            get => _height;
            set { _height = value; MoveOrigin(GeoreferenceOriginAuthority.LongitudeLatitudeHeight); }
        }

        public double scale
        {
            get => _scale;
            set { _scale = value <= 0.0 ? 1.0 : value; MoveOrigin(_originAuthority); }
        }

        public double3 longitudeLatitudeHeight
        {
            get => new(_longitude, _latitude, _height);
            set
            {
                _longitude = value.x;
                _latitude = value.y;
                _height = value.z;
                MoveOrigin(GeoreferenceOriginAuthority.LongitudeLatitudeHeight);
            }
        }

        public double3 ecefOrigin
        {
            get => new(_ecefX, _ecefY, _ecefZ);
            set
            {
                _ecefX = value.x;
                _ecefY = value.y;
                _ecefZ = value.z;
                MoveOrigin(GeoreferenceOriginAuthority.EarthCenteredEarthFixed);
            }
        }

        public double4x4 localToEcefMatrix
        {
            get { EnsureInitialized(); return _localToEcef; }
        }

        public double4x4 ecefToLocalMatrix
        {
            get { EnsureInitialized(); return _ecefToLocal; }
        }

        private void Awake() => EnsureInitialized();

        private void OnValidate()
        {
            _initialized = false;
            EnsureInitialized();
        }

        public double3 TransformEarthCenteredEarthFixedPositionToUnity(double3 ecefPosition)
        {
            EnsureInitialized();
            return math.mul(_ecefToLocal, new double4(ecefPosition, 1.0)).xyz;
        }

        public double3 TransformUnityPositionToEarthCenteredEarthFixed(double3 unityPosition)
        {
            EnsureInitialized();
            return math.mul(_localToEcef, new double4(unityPosition, 1.0)).xyz;
        }

        public double3 TransformEarthCenteredEarthFixedDirectionToUnity(double3 ecefDirection)
        {
            EnsureInitialized();
            return math.mul(_ecefToLocal, new double4(ecefDirection, 0.0)).xyz;
        }

        public void Register(GlobeAnchor anchor)
        {
            if (!_anchors.Contains(anchor)) _anchors.Add(anchor);
        }

        public void Unregister(GlobeAnchor anchor) => _anchors.Remove(anchor);

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            Recompute(_originAuthority);
        }

        private void MoveOrigin(GeoreferenceOriginAuthority authority)
        {
            for (int i = 0; i < _anchors.Count; i++)
                if (_anchors[i] != null) _anchors[i].SyncFromTransform();

            _initialized = true;
            Recompute(authority);

            for (int i = _anchors.Count - 1; i >= 0; i--)
            {
                if (_anchors[i] == null) _anchors.RemoveAt(i);
                else _anchors[i].ApplyToTransform();
            }
        }

        private void Recompute(GeoreferenceOriginAuthority authority)
        {
            _originAuthority = authority;

            if (authority == GeoreferenceOriginAuthority.LongitudeLatitudeHeight)
            {
                var ecef = Wgs84.LongitudeLatitudeHeightToEcef(new double3(_longitude, _latitude, _height));
                _ecefX = ecef.x;
                _ecefY = ecef.y;
                _ecefZ = ecef.z;
            }
            else
            {
                var llh = Wgs84.EcefToLongitudeLatitudeHeight(new double3(_ecefX, _ecefY, _ecefZ));
                _longitude = llh.x;
                _latitude = llh.y;
                _height = llh.z;
            }

            if (_scale <= 0.0) _scale = 1.0;

            _localToEcef = Wgs84.EastUpNorthToEcef(
                new double3(_ecefX, _ecefY, _ecefZ),
                new double3(_longitude, _latitude, _height),
                _scale);
            _ecefToLocal = math.inverse(_localToEcef);
        }
    }
}
