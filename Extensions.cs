using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectScheduling
{
	public static class Extensions
	{
		public static double NextGaussian( this Random self, double mean, double stdDev )
		{
			return mean + stdDev * self.NextNormal();
		}

		/// <summary>
		/// Returns a random number in range -1 to 1.
		/// </summary>
		public static double NextNormal( this Random self )
		{
			double u1 = self.NextDouble();
			double u2 = self.NextDouble();
			return Math.Sqrt( -2.0 * Math.Log( u1 ) ) *
					Math.Sin( 2.0 * Math.PI * u2 );
		}

		/// <summary>
		/// Returns a random boolean value.
		/// </summary>
		public static bool NextBool( this Random self )
		{
			return self.Next( 0, 2 ) == 0;
		}

		public static T From<T>( this Random self, ICollection<T> c )
		{
			return c.ElementAt( self.Next( 0, c.Count ) );
		}

		public static IEnumerable<int> FindAllIndices<T>( this IList<T> self, Predicate<T> p )
		{
			List<int> result = new List<int>();

			for ( int i = 0; i < self.Count; ++i ) {
				if ( p( self[i] ) ) {
					result.Add( i );
				}
			}

			return result;
		}
	}
}
