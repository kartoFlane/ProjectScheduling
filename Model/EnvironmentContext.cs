﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneticAlgorithm.Model
{
	public class EnvironmentContext
	{
		private static IEnumerable<int> _initialCapacityList;
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
		public bool AlgorithmicPrerequisites { get; set; }
		public double PenaltyPrerequisite { get; set; }

		#endregion

		#region Environment Properties

		public double TimeWeight { get; set; }
		public int Cheapest { get; set; }
		public double MinCost { get; set; }
		public double MaxCost { get; set; }
		public int TimeMax { get; set; }

		public Dictionary<int, Resource> Resources { get; private set; }
		public Dictionary<int, Task> Tasks { get; private set; }

		#endregion

		public Random Random { get; private set; }


		public EnvironmentContext()
		{
			Resources = new Dictionary<int, Resource>();
			Tasks = new Dictionary<int, Task>();
			Random = new Random();

			_initialCapacityList = Enumerable.Range( 0, Tasks.Count );
		}

		#region Population Generation / Mutation

		public int RandomResourceId( int taskId )
		{
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
			} );

			if ( resSet.Count == 0 ) {
				throw new Exception( "DEF error: no resources can do task #" + taskId + "!" );
			}

			return Random.From( resSet ).Id;
		}

		public int RandomPriority( int curValue = -1 )
		{
			if ( curValue < 0 ) {
				return Random.Next( 0, Tasks.Count );
			}
			else {
				return (int)Math.Max( 0, Math.Min( Tasks.Count - 1,
					Math.Round( Random.NextGaussian( curValue, Tasks.Count / 2 ) ) ) );
			}
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
			// Preallocate space in the collections. Saves ~6% of runtime.
			Dictionary<int, int> busyResourceMap = new Dictionary<int, int>( Resources.Count );
			Dictionary<int, int> resourceTaskMap = new Dictionary<int, int>( Resources.Count );

			foreach ( int rId in Resources.Keys ) {
				busyResourceMap[rId] = -1;
				resourceTaskMap[rId] = -1;
			}

			// HashSet doesn't expose an initial capacity constructor.
			// Requires abusing implementation detail of the class to set it.
			HashSet<int> completedTasks = new HashSet<int>( _initialCapacityList );
			completedTasks.Clear();

			int currentTime = 0;
			double penalty = 0;

			List<TaskData> pendingTasks = specimen.Genotype.OrderBy( td => td.Priority ).ToList();

			// TODO: Optionally sort by time to complete, cost, or pick randomly here,
			// for tasks with the same priority.

			while ( completedTasks.Count < Tasks.Count ) {
				++currentTime;

				// Check whether any tasks have been completed at this time step.
				bool allBusy = true;
				foreach ( int rId in Resources.Keys ) {
					int taskDoneTime = busyResourceMap[rId];

					if ( taskDoneTime < 0 ) {
						// A resource is idle
						allBusy = false;
					}
					else if ( taskDoneTime <= currentTime ) {
						completedTasks.Add( resourceTaskMap[rId] );
						busyResourceMap[rId] = -1;
						resourceTaskMap[rId] = -1;

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
					Task t = Tasks[td.TaskId];

					if ( AlgorithmicPrerequisites && !t.Predecessors.All( p => completedTasks.Contains( p ) ) ) {
						continue;
					}

					if ( busyResourceMap[td.ResourceId] >= 0 ) {
						// Resource is busy, we can't complete this task at this point in time.
						continue;
					}

					// We can use the resource, so mark it as busy
					busyResourceMap[td.ResourceId] = currentTime + t.Duration;
					resourceTaskMap[td.ResourceId] = td.TaskId;

					if ( !AlgorithmicPrerequisites ) {
						// Apply penalty for missing predecessor tasks
						foreach ( int reqId in t.Predecessors ) {
							if ( !completedTasks.Contains( reqId ) ) {
								penalty += PenaltyPrerequisite;
							}
						}
					}

					pendingTasks.Remove( td );
					--i;
				}
			}

			double time = currentTime / (double)TimeMax;

			return time + penalty;
		}
	}
}
