
namespace GeneticAlgorithm.Model
{
	public class TaskData
	{
		public readonly int taskId;

		public int ResourceId { get; set; }

		public int Priority { get; set; }

		public TaskData( int tId, int rId, int p )
		{
			taskId = tId;
			ResourceId = rId;
			Priority = p;
		}

		public bool Mutate( EnvironmentContext env, double overrideMutationChance )
		{
			bool changed = false;

			if ( env.Random.NextDouble() < overrideMutationChance ) {
				int old = ResourceId;
				ResourceId = env.RandomResourceId( taskId );
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
			return string.Format( "( id: {0}, resId: {1}, p: {2} )", taskId, ResourceId, Priority );
		}
	}
}
