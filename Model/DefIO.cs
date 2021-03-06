﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectScheduling.Model
{
	public static class DefIO
	{
		public static void ReadDEF( EnvironmentContext env, string path )
		{
			if ( !File.Exists( path ) )
				throw new ArgumentException( "File not found: " + path );

			string content = File.ReadAllText( path );

			const string sectionPtrn = @"=+([^=]+)";
			const string floatPtrn = @"\d+(.\d+)?";
			const string skillPtrn = @"\s*?Q(\d+):\s*?(\d+)\s*?";
			string resPtrn = Group( "\\d+" ) + "\\s*?" + Group( floatPtrn ) +
				"\\s*?" + Group( skillPtrn, "*" ) + EndOfLine();
			string taskPtrn = Group( "\\d+" ) + "\\s*?" + Group( "\\d+" ) +
				"\\s*?" + Group( skillPtrn, "*" ) +
				Group( "\\s*?\\d+", "*" ) + EndOfLine();

			List<Resource> resources = new List<Resource>();
			List<Task> tasks = new List<Task>();

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

				resources.Add( new Resource( id, cost, skillPool ) );
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

				tasks.Add( new Task( id, duration, skillReqs, prereqs ) );
			}

			env.Load( resources, tasks );

			env.ComputeCache();
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

			// Preallocate space in the collections. Saves ~6% of runtime.
			Dictionary<int, int> busyResourceMap = new Dictionary<int, int>();
			Dictionary<int, int> resourceTaskMap = new Dictionary<int, int>();

			foreach ( Resource r in env.Resources ) {
				busyResourceMap[r.Id] = -1;
				resourceTaskMap[r.Id] = -1;
			}

			HashSet<int> completedTasks = new HashSet<int>();

			List<string> prereqs = new List<string>();

			int offset = env.Tasks.Length;
			List<TaskData> pendingTasks = new List<TaskData>( offset );
			for ( int i = 0; i < offset; ++i ) {
				pendingTasks.Add( new TaskData( i, specimen.Genotype[i], specimen.Genotype[i + offset] ) );
			}
			pendingTasks = pendingTasks.OrderBy( td => td.Priority ).ToList();

			int currentTime = 0;
			double totalCost = 0;

			while ( completedTasks.Count < env.Tasks.Length ) {
				bool newRow = false;
				++currentTime;

				// Check whether any tasks have been completed at this time step.
				foreach ( Resource r in env.Resources ) {
					int taskDoneTime = busyResourceMap[r.Id];

					if ( taskDoneTime < 0 ) {
					}
					else if ( taskDoneTime <= currentTime ) {
						Task t = env.Tasks[resourceTaskMap[r.Id]];
						totalCost += t.Duration * r.Cost;

						completedTasks.Add( resourceTaskMap[r.Id] );
						busyResourceMap[r.Id] = -1;
						resourceTaskMap[r.Id] = -1;
					}
				}

				for ( int i = 0; i < pendingTasks.Count; ++i ) {
					TaskData td = pendingTasks[i];

					Resource r = env.Resources[td.ResourceId];
					Task t = env.Tasks[td.taskId];

					if ( !t.Predecessors.All( p => completedTasks.Contains( p ) ) ) {
						continue;
					}

					if ( busyResourceMap[td.ResourceId] >= 0 ) {
						// Resource is busy, we can't complete this task at this point in time.
						continue;
					}

					// We can use the resource, so mark it as busy
					busyResourceMap[td.ResourceId] = currentTime + t.Duration;
					resourceTaskMap[td.ResourceId] = td.taskId;

					pendingTasks.Remove( td );
					--i;

					if ( !newRow ) {
						newRow = true;
						buf.Append( currentTime ).Append( " " );
					}

					buf.Append( td.ResourceId + 1 ).Append( "-" ).Append( td.taskId + 1 );
					buf.Append( " " );
				}

				if ( newRow )
					buf.Append( "\n" );
			}

			if ( debug ) {
				buf.Append( "\nDebug\n" ).Append( "===================================\n" );
				buf.Append( "\tTime: " ).Append( currentTime ).Append( '\n' );
				buf.Append( "\tCost: " ).Append( totalCost ).Append( '\n' );
				buf.Append( "\nPriorities\n" ).Append( "===================================\n" );
				for ( int i = 0; i < offset; ++i ) {
					buf.AppendFormat( "\t{0} - {1}\n", i, specimen.Genotype[i + offset] );
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
