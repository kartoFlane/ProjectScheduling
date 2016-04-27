﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GeneticAlgorithm.Model
{
	public static class DefIO
	{
		public static void ReadDEF( EnvironmentContext env, string path )
		{
			if ( !File.Exists( path ) )
				throw new ArgumentException( "File not found: " + path );

			env.Resources.Clear();
			env.Tasks.Clear();

			string content = File.ReadAllText( path );

			const string sectionPtrn = @"=+([^=]+)";
			const string floatPtrn = @"\d+(.\d+)?";
			const string skillPtrn = @"\s*?Q(\d+):\s*?(\d+)\s*?";
			string resPtrn = Group( "\\d+" ) + "\\s*?" + Group( floatPtrn ) +
				"\\s*?" + Group( skillPtrn, "*" ) + EndOfLine();
			string taskPtrn = Group( "\\d+" ) + "\\s*?" + Group( "\\d+" ) +
				"\\s*?" + Group( skillPtrn, "*" ) +
				Group( "\\s*?\\d+", "*" ) + EndOfLine();

			Match m = null;
			MatchCollection mc = null;

			// File metadata, ignore
			m = Regex.Match( content, sectionPtrn );

			// General characteristics, ignore
			m = m.NextMatch();

			// Resource table
			m = m.NextMatch();
			mc = Regex.Matches( m.Value, resPtrn );
			foreach ( Match mm in mc ) {
				int id = int.Parse( mm.Groups[1].Value ) - 1;
				double cost = double.Parse( mm.Groups[2].Value, CultureInfo.InvariantCulture );

				Dictionary<int, int> skillPool = new Dictionary<int, int>();
				foreach ( Match mmm in Regex.Matches( mm.Value, skillPtrn ) ) {
					int skillId = int.Parse( mmm.Groups[1].Value );
					int skillReq = int.Parse( mmm.Groups[2].Value );
					skillPool.Add( skillId, skillReq );
				}

				env.Resources.Add( id, new Resource( id, cost, skillPool ) );
			}

			// Task table
			m = m.NextMatch();
			mc = Regex.Matches( m.Value, taskPtrn );
			foreach ( Match mm in mc ) {
				int id = int.Parse( mm.Groups[1].Value ) - 1;
				int duration = int.Parse( mm.Groups[2].Value );

				Dictionary<int, int> skillReqs = new Dictionary<int, int>();
				string v = mm.Groups[3].Value;
				foreach ( Match mmm in Regex.Matches( v, skillPtrn ) ) {
					int skillId = int.Parse( mmm.Groups[1].Value );
					int skillReq = int.Parse( mmm.Groups[2].Value );
					skillReqs.Add( skillId, skillReq );
				}

				List<int> prereqs = new List<int>();
				v = mm.Groups[7].Value;
				foreach ( Match mmm in Regex.Matches( v, "\\d+" ) ) {
					int preId = int.Parse( mmm.Value ) - 1;
					prereqs.Add( preId );
				}

				env.Tasks.Add( id, new Task( id, duration, skillReqs, prereqs ) );
			}
		}

		public static void WriteDEF(
			EnvironmentContext env,
			ProjectSchedule specimen,
			string path,
			bool debug )
		{
			StringBuilder buf = new StringBuilder();

			buf.Append( "Hour " ).Append( "\t" );
			buf.Append( " Resource assignments (resource ID - task ID) " );
			buf.Append( "\n" );

			Dictionary<int, int> busyResourceMap = new Dictionary<int, int>();
			Dictionary<int, int> resourceTaskMap = new Dictionary<int, int>();
			HashSet<int> completedTasks = new HashSet<int>();

			List<string> prereqs = new List<string>();

			int currentTime = 0;
			double totalCost = 0;

			while ( completedTasks.Count < env.Tasks.Count ) {
				bool newRow = false;
				++currentTime;

				// Check whether any tasks have been completed at this time step.
				foreach ( int rId in env.Resources.Keys ) {
					if ( busyResourceMap.ContainsKey( rId ) && busyResourceMap[rId] <= currentTime ) {
						Task t = env.Tasks[resourceTaskMap[rId]];
						Resource r = env.Resources[rId];
						totalCost += t.Duration * r.Cost;

						completedTasks.Add( resourceTaskMap[rId] );
						busyResourceMap.Remove( rId );
						resourceTaskMap.Remove( rId );
					}
				}

				for ( int curPrio = 0; curPrio < env.Tasks.Count; ++curPrio ) {
					// Get tasks at current priority level, or higher...
					IEnumerable<int> pendingTasks = specimen.GetTasksWithPriority( curPrio );
					// ...that haven't been completed yet / aren't currently being processed.
					pendingTasks = pendingTasks.Except( completedTasks ).Except( resourceTaskMap.Values );

					// TODO: Optionally sort by time to complete, cost, or pick randomly here,
					// for tasks with the same priority.

					if ( env.AlgorithmicPrerequisites ) {
						pendingTasks = pendingTasks.Where( e =>
						{
							Task t = env.Tasks[e];
							return t.Predecessors.All( p => completedTasks.Contains( p ) );
						} );
					}

					foreach ( int taskId in pendingTasks ) {
						TaskData td = specimen.Genotype[taskId];

						if ( busyResourceMap.ContainsKey( td.ResourceId ) ) {
							// Resource is busy, we can't complete this task at this point in time.
							continue;
						}

						// We can use the resource, so mark it as busy
						Resource r = env.Resources[td.ResourceId];
						Task t = env.Tasks[td.TaskId];

						busyResourceMap.Add( td.ResourceId, currentTime + t.Duration );
						resourceTaskMap.Add( td.ResourceId, td.TaskId );

						foreach ( int reqId in t.Predecessors ) {
							if ( !completedTasks.Contains( reqId ) ) {
								string s = "";
								foreach ( int id in t.Predecessors )
									s += ( id + 1 ) + ", ";
								prereqs.Add( string.Format( "{0}\t{1}: {2} (reqs: {3})",
									currentTime, t.Id, reqId + 1, s ) );
							}
						}

						if ( !newRow ) {
							newRow = true;
							buf.Append( currentTime ).Append( " " );
						}

						buf.Append( td.ResourceId + 1 ).Append( "-" ).Append( td.TaskId + 1 );
						buf.Append( " " );
					}
				}

				if ( newRow )
					buf.Append( "\n" );
			}

			if ( debug ) {
				buf.Append( "\nDebug\n" ).Append( "===================================\n" );
				buf.Append( "Time: " ).Append( currentTime ).Append( '\n' );
				buf.Append( "Cost: " ).Append( totalCost ).Append( '\n' );
				buf.Append( "Prerequisites:\n" ).Append( "===================================\n" );
				foreach ( string s in prereqs ) {
					buf.Append( s ).Append( "\n" );
				}
			}

			File.WriteAllText( path, buf.ToString() );
		}

		private static string Group( string str )
		{
			return "(" + str + ")";
		}

		private static string Group( string str, string quant )
		{
			return "(" + Group( str ) + quant + ")";
		}

		private static string EndOfLine()
		{
			return "\\s*?[\r|\n|\r\n]";
		}
	}
}