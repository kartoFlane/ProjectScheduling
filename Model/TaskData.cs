
namespace GeneticAlgorithm.Model
{
	public class TaskData
	{
		public int TaskId { get; private set; }

		public int ResourceId { get; set; }

		public int Priority { get; set; }

		public TaskData( int tId, int rId, int p )
		{
			TaskId = tId;
			ResourceId = rId;
			Priority = p;
		}

		public bool Mutate( EnvironmentContext env, double overrideMutationChance )
		{
			bool changed = false;

			if ( env.Random.NextDouble() < overrideMutationChance ) {
				int old = ResourceId;
				ResourceId = env.RandomResourceId( TaskId );
				changed |= old != ResourceId;
			}

			if ( env.Random.NextDouble() < overrideMutationChance ) {
				int old = Priority;
				Priority = env.RandomPriority( Priority );
				changed |= old != Priority;
			}

			return changed;
		}

		public Resource GetResource( EnvironmentContext env )
		{
			return env.Resources[ResourceId];
		}

		public override string ToString()
		{
			return string.Format( "( id: {0}, resId: {1}, p: {2} )", TaskId, ResourceId, Priority );
		}
	}
}
