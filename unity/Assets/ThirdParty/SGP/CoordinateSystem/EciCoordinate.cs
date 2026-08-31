using System;
using System.Collections.Generic;
using Satellites.SGP.Propagation;
using Satellites.SGP.Util;

namespace Satellites.SGP.CoordinateSystem
{
    /// <inheritdoc />
    /// <summary>
    ///     Stores an Earth-centered inertial position for a particular time
    /// </summary>
    public struct EciCoordinate
    {
        private static readonly int[] LocCharRangeAaXx = { 18, 10, 24, 10, 24, 10 };
		private static readonly int[] LocCharRangeAaYy = { 18, 10, 24, 10, 25, 10 };

		/// <summary>
		///     Converts this coordinate to its Maidenhead Locator System representation, disregarding altitude
		/// </summary>
		/// <param name="precision">The precision of the conversion, which defines the number of pairs in the conversion</param>
		/// <param name="standard">The conversion standard to use for the 5th pair</param>
		/// <returns>The Maidenhead representation string</returns>
		public string ToMaidenhead(MaidenheadPrecision precision = MaidenheadPrecision.FiveKilometers,
			MaidenheadStandard standard = MaidenheadStandard.AaToXx)
		{
			var geo = ToGeodetic();
			var pairCount = (int)precision + 1;

			var locator = new char[pairCount * 2];
			int[] charRange;

			switch (standard)
			{
				case MaidenheadStandard.AaToXx:
					charRange = LocCharRangeAaXx;
					break;
				case MaidenheadStandard.AaToYy:
					charRange = LocCharRangeAaYy;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(standard), standard, null);
			}

			for (var xOrY = 0; xOrY < 2; ++xOrY)
			{
				var ordinate = xOrY == 0
					? geo.Longitude.Degrees / 2.0
					: geo.Latitude.Degrees;
				var divisions = 1;

				/* The 1e-6 here guards against floating point rounding errors */
				ordinate += 270.000001 % 180.0;
				for (var pair = 0; pair < pairCount; ++pair)
				{
					divisions *= charRange[pair];
					var squareSize = 180.0 / divisions;

					var locvalue = (char)(ordinate / squareSize);
					ordinate -= squareSize * locvalue;
					locvalue += charRange[pair] == 10 ? '0' : 'A';
					locator[pair * 2 + xOrY] = locvalue;
				}
			}

			return new string(locator);
		}

		/// <summary>
		///     Converts this coordinate to its Degrees-Minutes-Seconds (DMS) representation, disregarding altitude
		/// </summary>
		/// <returns>The Degrees-Minutes-Seconds representation string</returns>
		public string ToDegreesMinutesSeconds()
		{
			var geo = ToGeodetic();

			var north = geo.Latitude > Angle.Zero;
			var east = geo.Longitude > Angle.Zero;

			var latd = Angle.FromDegrees(Math.Abs(geo.Latitude.Degrees));
			var lond = Angle.FromDegrees(Math.Abs(geo.Longitude.Degrees));

			return $"{latd.ToDegreesMinutesSeconds()}\"{(north ? "N" : "S")} {lond.ToDegreesMinutesSeconds()}\"{(east ? "E" : "W")}";
		}

		/// <summary>
		///     Converts this coordinate to an ECEF one, assuming a spherical earth
		/// </summary>
		/// <returns>A spherical ECEF coordinate vector</returns>
		public Vector3 ToSphericalEcef()
		{
			var geo = ToGeodetic();
			return new Vector3(
				Math.Cos(geo.Latitude.Radians) * Math.Cos(-geo.Longitude.Radians + Math.PI) *
				(geo.Altitude + SgpConstants.EarthRadiusKm),
				Math.Sin(geo.Latitude.Radians) * (geo.Altitude + SgpConstants.EarthRadiusKm),
				Math.Cos(geo.Latitude.Radians) * Math.Sin(-geo.Longitude.Radians + Math.PI) *
				(geo.Altitude + SgpConstants.EarthRadiusKm)
			);
		}

		/// <summary>
		///     Calculates the visibility radius (km) of the satellite by which any distances from this coordinate less than the
		///     radius are able to see this coordinate
		/// </summary>
		/// <returns>The visibility radius, in kilometers</returns>
		public double GetFootprint()
		{
			return GetFootprintAngle().Radians * SgpConstants.EarthRadiusKm;
		}

		/// <summary>
		///     Calculates the visibility radius (radians) of the satellite by which any distances from this coordinate less than
		///     the
		///     radius are able to see this coordinate
		/// </summary>
		/// <returns>The visibility radius as an angle across Earth's surface</returns>
		public Angle GetFootprintAngle()
		{
			var geo = ToGeodetic();
			return Angle.FromRadians(Math.Acos(SgpConstants.EarthRadiusKm / (SgpConstants.EarthRadiusKm + geo.Altitude)));
		}

		/// <summary>
		///     Gets a list of geodetic coordinates which define the bounds of the visibility footprint at a specific time
		/// </summary>
		/// <param name="numPoints">The number of points in the resulting circle</param>
		/// <returns>A list of geodetic coordinates for the specified time</returns>
		public List<GeodeticCoordinate> GetFootprintBoundary(int numPoints = 60)
		{
			var center = ToGeodetic();
			var coords = new List<GeodeticCoordinate>();

			var lat = center.Latitude;
			var lon = center.Longitude;
			var d = center.GetFootprintAngle().Radians;

			for (var i = 0; i < numPoints; i++)
			{
				var perc = i / (float)numPoints * 2 * Math.PI;

				var latRadians = Math.Asin(Math.Sin(lat.Radians) * Math.Cos(d) +
				                           Math.Cos(lat.Radians) * Math.Sin(d) * Math.Cos(perc));
				var lngRadians = lon.Radians +
				                 Math.Atan2(Math.Sin(perc) * Math.Sin(d) * Math.Cos(lat.Radians),
					                 Math.Cos(d) - Math.Sin(lat.Radians) * Math.Sin(latRadians));

				lngRadians = MathUtil.WrapNegPosPi(lngRadians);

				coords.Add(new GeodeticCoordinate(Angle.FromRadians(latRadians), Angle.FromRadians(lngRadians), 10));
			}

			return coords;
		}

		/// <summary>
		///     Calculates the Great Circle distance (km) to another coordinate
		/// </summary>
		/// <param name="to">The coordinate to measure against</param>
		/// <returns>The distance between the coordinates, in kilometers</returns>
		public double DistanceTo(EciCoordinate to)
		{
			return AngleTo(to).Radians * SgpConstants.EarthRadiusKm;
		}

		/// <summary>
		///     Calculates the Great Circle distance as an angle to another geodetic coordinate, ignoring altitude
		/// </summary>
		/// <param name="to">The coordinate to measure against</param>
		/// <returns>The distance between the coordinates as an angle across Earth's surface</returns>
		public Angle AngleTo(EciCoordinate to)
		{
			var geo = ToGeodetic();
			var toGeo = to.ToGeodetic();
			var dist = Math.Sin(geo.Latitude.Radians) * Math.Sin(toGeo.Latitude.Radians) +
			           Math.Cos(geo.Latitude.Radians) * Math.Cos(toGeo.Latitude.Radians) *
			           Math.Cos(geo.Longitude.Radians - toGeo.Longitude.Radians);
			dist = Math.Acos(dist);

			return Angle.FromRadians(dist);
		}

		/// <summary>
		///     Returns true if there is line-of-sight between this coordinate and the supplied one by checking if this coordinate
		///     is within the footprint of the other
		/// </summary>
		/// <param name="other">The coordinate to check against</param>
		/// <returns>True if there is line-of-sight between this coordinate and the supplied one</returns>
		public bool CanSee(EciCoordinate other)
		{
			return AngleTo(other) < other.GetFootprintAngle();
		}

        /// <summary>
        ///     The time component of the coordinate
        /// </summary>
        public DateTime Time { get; }

        /// <summary>
        ///     The position component of the coordinate
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        ///     The velocity component of the coordinate
        /// </summary>
        public Vector3 Velocity { get; }

        /// <inheritdoc />
        /// <summary>
        ///     Creates a new ECI coordinate with the specified values
        /// </summary>
        /// <param name="dt">The date to be used for this position</param>
        /// <param name="latitude">The latitude</param>
        /// <param name="longitude">The longitude</param>
        /// <param name="altitude">The altitude in kilometers</param>
        public EciCoordinate(DateTime dt, Angle latitude, Angle longitude, double altitude)
        {
	        this = new GeodeticCoordinate(latitude, longitude, altitude).ToEci(dt);
        }

        /// <summary>
        ///     Creates a new ECI coordinate with the specified values
        /// </summary>
        /// <param name="dt">The date to be used for this position</param>
        /// <param name="coord">The position top copy</param>
        public EciCoordinate(DateTime dt, EciCoordinate coord)
        {
            dt = dt.ToStrictUtc();
            var eci = coord.ToEci(dt);

            Time = dt;
            Position = eci.Position;
            Velocity = eci.Velocity;
        }

        /// <inheritdoc />
        public EciCoordinate(DateTime dt, Vector3 position) : this(dt, position, new Vector3())
        {
        }

        /// <summary>
        ///     Creates a new ECI coordinate with the specified values
        /// </summary>
        /// <param name="dt">The date to be used for this position</param>
        /// <param name="position">The ECI position vector</param>
        /// <param name="velocity">The ECI velocity vector</param>
        public EciCoordinate(DateTime dt, Vector3 position, Vector3 velocity)
        {
            Time = dt.ToStrictUtc();
            Position = position;
            Velocity = velocity;
        }

        /// <inheritdoc />
        /// <summary>
        ///     Converts this ECI position to a geodetic one
        /// </summary>
        /// <returns>The position in a geodetic reference frame</returns>
        public GeodeticCoordinate ToGeodetic()
        {
            var theta = MathUtil.AcTan(Position.Y, Position.X);

            var lon = MathUtil.WrapNegPosPi(theta - Time.ToGreenwichSiderealTime());

            var r = Math.Sqrt(Position.X * Position.X + Position.Y * Position.Y);

            const double e2 = SgpConstants.EarthFlatteningConstant * (2.0 - SgpConstants.EarthFlatteningConstant);

            var lat = MathUtil.AcTan(Position.Z, r);
            double phi;
            double c;
            var cnt = 0;

            do
            {
                phi = lat;
                var sinphi = Math.Sin(phi);
                c = 1.0 / Math.Sqrt(1.0 - e2 * sinphi * sinphi);
                lat = MathUtil.AcTan(Position.Z + SgpConstants.EarthRadiusKm * c * e2 * sinphi, r);
                cnt++;
            } while (Math.Abs(lat - phi) >= 1e-10 && cnt < 10);

            var alt = r / Math.Cos(lat) - SgpConstants.EarthRadiusKm * c;

            return new GeodeticCoordinate(Angle.FromRadians(lat), Angle.FromRadians(lon), alt);
        }

        /// <inheritdoc />
        public EciCoordinate ToEci(DateTime dt)
        {
            dt = dt.ToStrictUtc();
            // Can't directly compare dates here because the round-trip conversion from
            // the observation DateTime to total seconds since the epoch back to
            // the DateTime given to this EciCoordinate is lossy by about 10 microseconds
            return Math.Abs((dt - Time).TotalMilliseconds) < 1 ? this : ToGeodetic().ToEci(dt);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"EciCoordinate[Position={Position}, Velocity={Velocity}]";
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hashCode = 818017616;
            hashCode = hashCode * -1521134295 + Time.GetHashCode();
            hashCode = hashCode * -1521134295 + Position.GetHashCode();
            hashCode = hashCode * -1521134295 + Velocity.GetHashCode();
            return hashCode;
        }

        /// <inheritdoc />
        public bool Equals(EciCoordinate other)
        {
            return base.Equals(other) && Time.Equals(other.Time) && Equals(Position, other.Position) &&
                   Equals(Velocity, other.Velocity);
        }

        /// <inheritdoc />
        public static bool operator ==(EciCoordinate left, EciCoordinate right)
        {
            return Equals(left, right);
        }

        /// <inheritdoc />
        public static bool operator !=(EciCoordinate left, EciCoordinate right)
        {
            return !Equals(left, right);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is EciCoordinate eci && Equals(eci);
        }
    }
}