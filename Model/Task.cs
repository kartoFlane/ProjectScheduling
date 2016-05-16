using System.Collections.Generic;
using System.Text;

namespace ProjectScheduling.Model
{
	public class Task
	{
		public int Id { get; private set; }

		public int Duration { get; private set; }

		public Dictionary<int, int> SkillRequirements { get; private set; }

		public int[] Predecessors { get; private set; }

		public Task( int id, int duration, Dictionary<int, int> skillReqs, List<int> pred )
		{
			Id = id;
			Duration = duration;
			SkillRequirements = new Dictionary<int, int>();

			foreach ( int skillKey in skillReqs.Keys ) {
				SkillRequirements.Add( skillKey, skillReqs[skillKey] );
			}

			Predecessors = pred.ToArray();
		}

		public override string ToString()
		{
			StringBuilder buf = new StringBuilder();

			buf.AppendFormat( "{0,-8}", Id );
			buf.AppendFormat( "{0,-8}", Duration );

			foreach ( int i in SkillRequirements.Keys ) {
				buf.Append( 'Q' ).Append( i ).Append( ": " );
				buf.Append( SkillRequirements[i] ).Append( "\t" );
			}

			foreach ( int i in Predecessors ) {
				buf.Append( i ).Append( "\t" );
			}

			return buf.ToString();
		}
	}
}
