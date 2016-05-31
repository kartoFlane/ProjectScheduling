using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectScheduling.Model
{
	public class EnvironmentContext
	{
		// Resources that can complete this task
		private static List<int>[] _taskResources;

		public double ProbabilityMutation { get; set; }
		public double ProbabilityOffspring { get; set; }

		public ECrossoverType CrossoverType { get; set; }

		#region Environment Properties

		public Resource[] Resources { get; private set; }
		public Task[] Tasks { get; private set; }

		#endregion

		public Random Random { get; private set; }


		public EnvironmentContext()
		{
			Random = new Random();
		}

		public void Load( List<Resource> res, List<Task> task )
		{
			Resources = res.ToArray();
			Tasks = task.ToArray();
		}

		public void ComputeCache()
		{
			_taskResources = new List<int>[Tasks.Length];

			foreach ( Task t in Tasks ) {

				var resSet = Resources.ToList().FindAll( r =>
				{
					bool result = true;
					foreach ( KeyValuePair<int, int> p in t.SkillRequirements ) {
						if ( !r.SkillPool.ContainsKey( p.Key ) )
							return false;

						result &= r.SkillPool[p.Key] >= p.Value;
					}
					return result;
				} ).Select( r => r.Id ).ToList();

				if ( resSet.Count == 0 ) {
					throw new Exception( "DEF error: no resources can do task #" + t.Id + "!" );
				}

				_taskResources[t.Id] = resSet;
			}
		}

		#region Population Generation / Mutation

		public int RandomResourceId( int taskId )
		{
			return Random.From( _taskResources[taskId] );
		}

		public int RandomPriority( int curValue = -1, int stdev = 5 )
		{
			return Random.Next( 0, Tasks.Length );
			/*
			else {
				double d = Random.NextGaussian( curValue, stdev );

				// Round away from 0, so that we always have a change if we do mutate
				if ( d > 0 && d < 1 )
					d = 1;
				if ( d < 0 && d > -1 )
					d = -1;
				if ( d == 0 )
					d = Random.NextBool() ? 1 : -1;

				return (int)Math.Max( 0, Math.Max( Tasks.Count, Math.Round( d ) ) );
			}
			*/
		}

		private int Clamp( int v, int min, int max )
		{
			return Math.Min( max, Math.Max( min, v ) );
		}

		public List<ProjectSchedule> GeneratePopulation( int count )
		{
			List<ProjectSchedule> result = new List<ProjectSchedule>();

			while ( result.Count < count ) {
				result.Add( new ProjectSchedule( this ) );
			}

			return result;
		}

		#endregion

		public double Evaluate( ProjectSchedule specimen )
		{
			int[] busyResources = new int[Resources.Length];
			int[] resourceTasks = new int[Resources.Length];
			bool[] completedTasks = new bool[Tasks.Length];

			foreach ( Resource r in Resources ) {
				busyResources[r.Id] = -1;
				resourceTasks[r.Id] = -1;
			}

			int completedTasksCount = 0;
			int currentTime = 0;
			double totalCost = 0;

			int offset = Tasks.Length;
			TaskData[] pendingTasks = new TaskData[offset];
			for ( int i = 0; i < offset; ++i ) {
				TaskData td = new TaskData( i, specimen.Genotype[i], specimen.Genotype[i + offset] );
				td.Task = Tasks[td.taskId];
				td.Resource = Resources[td.ResourceId];
				pendingTasks[i] = td;
			}

			pendingTasks = pendingTasks.OrderBy( td => td.Priority ).ToArray();

			while ( completedTasksCount < offset ) {
				++currentTime;

				// Check whether any tasks have been completed at this time step.
				bool allBusy = true;

				int earliest = int.MaxValue;
				foreach ( Resource r in Resources ) {
					int taskDoneTime = busyResources[r.Id];

					if ( taskDoneTime > 0 && taskDoneTime < earliest ) {
						earliest = taskDoneTime;
					}

					if ( taskDoneTime < 0 ) {
						// A resource is idle
						allBusy = false;
					}
					else if ( taskDoneTime <= currentTime ) {
						Task t = Tasks[resourceTasks[r.Id]];
						totalCost += t.Duration * r.Cost;

						completedTasks[t.Id] = true;
						completedTasksCount++;
						busyResources[r.Id] = -1;
						resourceTasks[r.Id] = -1;

						// A resource is being released
						allBusy = false;
					}
				}

				if ( allBusy ) {
					// If all resources are currently busy, then don't bother needlessly
					// looking for a task to insert.
					// Instead, skip to the time when a resource is freed.
					currentTime = earliest;
					continue;
				}

				for ( int i = 0; i < offset; ++i ) {
					TaskData td = pendingTasks[i];

					if ( td == null )
						continue;

					Resource r = Resources[td.ResourceId];
					Task t = Tasks[td.taskId];

					if ( busyResources[td.ResourceId] >= 0 ) {
						// Resource is busy, we can't complete this task at this point in time.
						continue;
					}

					if ( !t.Predecessors.All( p => completedTasks[p] ) ) {
						continue;
					}

					// We can use the resource, so mark it as busy
					busyResources[td.ResourceId] = currentTime + t.Duration;
					resourceTasks[td.ResourceId] = td.taskId;

					pendingTasks[i] = null;
				}
			}

			return currentTime + totalCost / 100000;
		}
	}
}
