using System.Collections.Generic;
using System.Text;

namespace ProjectScheduling.Model
{
	public class Resource
	{
		public int Id { get; private set; }

		public double Cost { get; private set; }

		public Dictionary<int, int> SkillPool { get; private set; }

		public Resource( int id, double cost, Dictionary<int, int> skillPool )
		{
			Id = id;
			Cost = cost;
			SkillPool = new Dictionary<int, int>();

			foreach ( int skillKey in skillPool.Keys ) {
				SkillPool.Add( skillKey, skillPool[skillKey] );
			}
		}

		public override string ToString()
		{
			StringBuilder buf = new StringBuilder();

			buf.AppendFormat( "{0,-8}", Id );
			buf.AppendFormat( "{0,-8}", Cost );

			foreach ( int i in SkillPool.Keys ) {
				buf.Append( 'Q' ).Append( i ).Append( ": " );
				buf.Append( SkillPool[i] ).Append( "\t" );
			}

			return buf.ToString();
		}
	}
}
