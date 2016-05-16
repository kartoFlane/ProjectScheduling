using System;

namespace ProjectScheduling.Model
{
	public class TaskData : IEquatable<TaskData>
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

		public TaskData( TaskData other )
		{
			taskId = other.taskId;
			ResourceId = other.ResourceId;
			Priority = other.Priority;
		}

		public TaskData Copy()
		{
			return new TaskData( this );
		}

		public bool Mutate( EnvironmentContext env, double overrideMutationChance, bool isClone )
		{
			bool changed = false;

			if ( env.Random.NextDouble() < overrideMutationChance ) {
				int old = ResourceId;
				ResourceId = env.RandomResourceId( taskId );
				changed |= old != ResourceId;
			}

			if ( env.Random.NextDouble() < overrideMutationChance ) {
				int old = Priority;
				int stdev = isClone ? env.Tasks.Length : 5;
				Priority = env.RandomPriority( Priority, stdev );
				changed |= old != Priority;
			}

			return changed;
		}

		public override string ToString()
		{
			return string.Format( "( id: {0}, resId: {1}, p: {2} )", taskId, ResourceId, Priority );
		}

		public bool Equals( TaskData other )
		{
			return taskId == other.taskId &&
				ResourceId == other.ResourceId &&
				Priority == other.Priority;
		}

		public override bool Equals( object obj )
		{
			if ( obj is TaskData )
				return Equals( (TaskData)obj );
			return false;
		}

		public override int GetHashCode()
		{
			int hash = 19;

			hash = hash * 31 + taskId.GetHashCode();
			hash = hash * 31 + ResourceId.GetHashCode();
			hash = hash * 31 + Priority.GetHashCode();

			return hash;
		}
	}
}
