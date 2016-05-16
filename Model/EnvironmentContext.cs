using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectScheduling.Model
{
	public class EnvironmentContext
	{
		private static Dictionary<int, List<int>> _taskResourceMap;

		#region Probabilities

		public double ProbabilityMutation { get; set; }
		public double ProbabilityOffspring { get; set; }

		#endregion

		#region Environment Functionalities

		/// <summary>
		/// Whether precedence relations should be handled with an algorithm
		/// that prevents erroneous assignments. If false, missing a precedence
		/// relation will result in penalty being applied instead.
		/// </summary>
		public bool AlgorithmicRelations { get; set; }
		public double PenaltyRelations { get; set; }
		public double PenaltyIdleResource { get; set; }
		public double PenaltyWaitingTask { get; set; }

		#endregion

		#region Environment Properties

		public Dictionary<int, Resource> Resources { get; private set; }
		public Dictionary<int, Task> Tasks { get; private set; }

		#endregion

		public Random Random { get; private set; }


		public EnvironmentContext()
		{
			Resources = new Dictionary<int, Resource>();
			Tasks = new Dictionary<int, Task>();
			Random = new Random();

			_taskResourceMap = new Dictionary<int, List<int>>();
		}

		public void ComputeTaskResourceCache()
		{
			foreach ( int taskId in Tasks.Keys ) {
				Task t = Tasks[taskId];

				var resSet = Resources.Values.ToList().FindAll( r =>
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
					throw new Exception( "DEF error: no resources can do task #" + taskId + "!" );
				}

				_taskResourceMap[taskId] = resSet;
			}
		}

		#region Population Generation / Mutation

		public int RandomResourceId( int taskId )
		{
			return Random.From( _taskResourceMap[taskId] );
		}

		public int RandomPriority( int curValue = -1, int stdev = 5 )
		{
			return Random.Next( 0, Tasks.Count );
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
			int[] busyResources = new int[Resources.Count];
			int[] resourceTasks = new int[Resources.Count];
			bool[] completedTasks = new bool[Tasks.Count];

			foreach ( int rId in Resources.Keys ) {
				busyResources[rId] = -1;
				resourceTasks[rId] = -1;
			}

			int completedTasksCount = 0;
			int currentTime = 0;
			double totalCost = 0;
			double penalty = 0;

			int offset = Tasks.Count;
			List<TaskData> pendingTasks = new List<TaskData>( offset );
			for ( int i = 0; i < offset; ++i ) {
				pendingTasks.Add( new TaskData( i, specimen.Genotype[i], specimen.Genotype[i + offset] ) );
			}
			pendingTasks = pendingTasks.OrderBy( td => td.Priority ).ToList();

			// TODO: Optionally sort by time to complete, cost, or pick randomly here,
			// for tasks with the same priority.

			while ( completedTasksCount < Tasks.Count ) {
				++currentTime;

				// Check whether any tasks have been completed at this time step.
				bool allBusy = true;
				foreach ( int rId in Resources.Keys ) {
					int taskDoneTime = busyResources[rId];

					if ( taskDoneTime < 0 ) {
						// A resource is idle
						allBusy = false;
					}
					else if ( taskDoneTime <= currentTime ) {
						Task t = Tasks[resourceTasks[rId]];
						Resource r = Resources[rId];
						totalCost += t.Duration * r.Cost;

						completedTasks[t.Id] = true;
						completedTasksCount++;
						busyResources[rId] = -1;
						resourceTasks[rId] = -1;

						// A resource is being released
						allBusy = false;
					}
				}

				if ( allBusy ) {
					// If all resources are currently busy, then don't bother needlessly
					// looking for a task to insert
					continue;
				}

				for ( int i = 0; i < pendingTasks.Count; ++i ) {
					TaskData td = pendingTasks[i];

					Resource r = Resources[td.ResourceId];
					Task t = Tasks[td.taskId];

					if ( AlgorithmicRelations && !t.Predecessors.All( p => completedTasks[p] ) ) {
						continue;
					}

					if ( busyResources[td.ResourceId] >= 0 ) {
						// Resource is busy, we can't complete this task at this point in time.
						// Apply penalty.
						penalty += PenaltyWaitingTask;
						continue;
					}

					// We can use the resource, so mark it as busy
					busyResources[td.ResourceId] = currentTime + t.Duration;
					resourceTasks[td.ResourceId] = td.taskId;

					if ( !AlgorithmicRelations ) {
						// Apply penalty for missing predecessor tasks
						foreach ( int reqId in t.Predecessors ) {
							if ( !completedTasks[reqId] ) {
								penalty += PenaltyRelations;
							}
						}
					}

					pendingTasks.Remove( td );
					--i;
				}

				// Apply penalty for each idle resource at each time step.
				foreach ( int rId in Resources.Keys ) {
					if ( busyResources[rId] == -1 ) {
						penalty += PenaltyIdleResource;
					}
				}
			}

			return currentTime + penalty + totalCost / 100000;
		}
	}
}
