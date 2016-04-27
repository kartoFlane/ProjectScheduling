using GeneticAlgorithm.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;


namespace GeneticAlgorithm
{
	public static class GeneticAlgorithm
	{
		// Initial parameters
		private const int _populationSize = 20;
		private const double _breederFraction = 1;
		private const double _cloneThreshold = 0.05;
		private const double _mutationChance = 0.03;
		private const double _crossoverChance = 0.4;

		private const bool _algorithmicPrerequisites = true;
		private const bool _cloneElimination = true;

		// Stop conditions
		private const int _generationLimit = 50;
		private const double _fitnessThreshold = 0.1;

		public static void Main()
		{
			Console.WriteLine( "Setting up environment..." );
			// Setup the environment for the genetic algorithm
			EnvironmentContext env = new EnvironmentContext();

			const bool debugOutput = true;
			const bool eliminateClones = _cloneElimination;

			//env.TimeWeight = 0.7;
			env.ProbabilityMutation = _mutationChance;
			env.ProbabilityOffspring = _crossoverChance;

			env.AlgorithmicPrerequisites = _algorithmicPrerequisites;
			env.PenaltyPrerequisite = 0.3;

			string[] files = Directory.GetFiles( "_defs/", "*.def" );
			Console.WriteLine( string.Format( "Found {0} .def files: ", files.Length ) );
			for ( int i = 0; i < files.Length; ++i ) {
				Console.WriteLine( string.Format( "{0,2}.\t{1}", i, new FileInfo( files[i] ).Name ) );
			}
			Console.WriteLine();

			int fileId = 2;

			if ( fileId == -1 ) {
				string input = Console.ReadLine();
				fileId = int.Parse( input );
			}

			FileInfo fi = new FileInfo( files[fileId] );
			Console.WriteLine( "Using " + fi.Name );
			DefIO.ReadDEF( env, fi.FullName );

			// Assertions
			if ( env.Resources.Count > 0 )
				Assert( env.Resources.Keys.Max() == env.Resources.Count - 1 );
			if ( env.Tasks.Count > 0 )
				Assert( env.Tasks.Keys.Max() == env.Tasks.Count - 1 );

			// Compute the maximum, theoretically possible time the project can take
			int totalTime = 0;
			foreach ( Task t in env.Tasks.Values )
				totalTime += t.Duration;
			env.TimeMax = totalTime;

			// Find the cheapest and most expensive Resources
			Resource rMin = null, rMax = null;
			double v = double.MaxValue;
			foreach ( Resource r in env.Resources.Values ) {
				if ( r.Cost < v ) {
					rMin = r;
					v = r.Cost;
				}
			}
			v = double.MinValue;
			foreach ( Resource r in env.Resources.Values ) {
				if ( r.Cost > v ) {
					rMax = r;
					v = r.Cost;
				}
			}

			env.Cheapest = rMin.Id;

			// Compute the minimum and maximum possible cost of the entire project
			foreach ( Task t in env.Tasks.Values ) {
				env.MinCost += t.Duration * rMin.Cost;
				env.MaxCost += t.Duration * rMax.Cost;
			}

			FitnessComparer comparer = new FitnessComparer( env );
			Dictionary<int, double> minMap = new Dictionary<int, double>();
			Dictionary<int, double> maxMap = new Dictionary<int, double>();
			Dictionary<int, double> avgMap = new Dictionary<int, double>();

			Console.Write( "Generating initial population..." );
			List<ProjectSchedule> population = env.GeneratePopulation( _populationSize );
			Console.WriteLine( " Done." );

			UpdateLogs( env, population, 0, minMap, maxMap, avgMap );

			ProjectSchedule allTimeBest = null;
			double startMin = minMap.Min( e => e.Value );
			double startMax = maxMap.Max( e => e.Value );

			Console.WriteLine();
			Console.WriteLine( "Started with --> Best: {0:F6} | Worst: {1:F6}", startMin, startMax );
			Console.WriteLine();

			int generationIndex = 0;
			int timeStart = Environment.TickCount;

			while ( minMap[generationIndex] > _fitnessThreshold && generationIndex < _generationLimit ) {
				int genTimeStart = Environment.TickCount;

				++generationIndex;
				Console.Write( "Generation: {0,4}", generationIndex );

				// Crossover
				var q = population.Take( (int)( _breederFraction * _populationSize ) ).ToList();

				Console.Write( "\t\tCO... " );
				//CrossOver_FalloffNormal( env, population, q );
				//CrossOver_FalloffLinear( env, population, q );
				//CrossOver_SimplePairs( env, population, q );
				CrossOver_EqualOpportunity( env, population, q );
				Console.Write( "Done, delta: {0,3}", population.Count - _populationSize );

				Console.Write( " | Mut... " );
				// Mutation
				population.ForEach( e => e.Mutate( env ) );
				Console.Write( "Done" );

				if ( eliminateClones ) {
					// Count clones
					Console.Write( "\t\tClones... " );
					double cc = population.Count - population.Distinct( comparer ).Count();
					if ( cc / _populationSize > _cloneThreshold ) {
						Console.Write( "\t\tDone, #: " + cc );
						// If clones make up more than 10% of the population, then start mutating them.
						Console.Write( " | Grouping... " );
						var c = population.GroupBy( spec => spec.GetFitness( env ) );
						Console.Write( "Done, Re-Mut... " );
						foreach ( var g in c ) {
							foreach ( ProjectSchedule clone in g.Skip( 1 ) ) {
								clone.Mutate( env, 10 * env.ProbabilityMutation );
							}
						}
						Console.Write( "Done" );
					}
					else {
						Console.Write( "\t\tDone, N/A" );
					}
				}

				// Selection
				// Find top (populationCount) specimens in order to keep the population stable
				Console.Write( "\t\tSelection... " );
				population = population
					.OrderBy( e => e.GetFitness( env ) )
					.Take( _populationSize )
					.ToList();
				Console.Write( "Done" );

				Console.Write( " | Logs... " );
				UpdateLogs( env, population, generationIndex, minMap, maxMap, avgMap );

				Console.Write( "\t\tMIN: {0:F6}", minMap[generationIndex] );
				Console.Write( "\t\tAVG: {0:F6}", avgMap[generationIndex] );
				Console.Write( "\t\tMAX: {0:F6}", maxMap[generationIndex] );

				// Find all-time best
				foreach ( ProjectSchedule specimen in population ) {
					if ( allTimeBest == null ||
							specimen.GetFitness( env ) < allTimeBest.GetFitness( env ) ) {
						allTimeBest = specimen.DeepCopy();
					}
				}

				Console.WriteLine( "\t\tTime/Gen: {0}s", ( Environment.TickCount - genTimeStart ) / 1000.0 );
			}

			Console.WriteLine();
			Console.WriteLine( "Done ==========================================" );
			Console.WriteLine( "Time: {0}s", ( Environment.TickCount - timeStart ) / 1000.0 );
			Console.WriteLine( "Started with --> Best: {0:F6} | Worst: {1:F6}", startMin, startMax );
			Console.WriteLine( "Ended with   --> Best: {0:F6} | Worst: {1:F6}",
				minMap.Min( e => e.Value ), maxMap.Max( e => e.Value ) );
			Console.WriteLine( "All-time best: {0:F6}", allTimeBest.GetFitness( env ) );

			File.Copy( fi.FullName, "_solutions/result.def", true );
			DefIO.WriteDEF( env, allTimeBest, "_solutions/result.sol", debugOutput );
			DumpLogs( "_solutions/dump.txt", minMap, maxMap, avgMap );
			DumpParams( "_solutions/params.txt", env );
		}

		// ==================================================================================================

		#region Crossover Strategies

		private static void CrossOver_SimplePairs(
			EnvironmentContext env,
			List<ProjectSchedule> population,
			List<ProjectSchedule> breeders )
		{
			for ( int i = 0; i < breeders.Count - 1; i += 2 ) {
				population.AddRange( breeders.ElementAt( i ).CrossOver( env, breeders.ElementAt( i + 1 ) ) );
			}
		}

		private static void CrossOver_EqualOpportunity(
			EnvironmentContext env,
			List<ProjectSchedule> population,
			List<ProjectSchedule> breeders )
		{
			for ( int i = 0; i < breeders.Count; ++i ) {
				for ( int j = 0; j < breeders.Count; ++j ) {
					if ( i == j )
						continue;

					population.AddRange( breeders.ElementAt( i ).CrossOver(
						env, breeders.ElementAt( j ) ) );
				}
			}
		}

		private static void CrossOver_FalloffLinear(
			EnvironmentContext env,
			List<ProjectSchedule> population,
			List<ProjectSchedule> breeders )
		{
			CrossOver_Falloff( env, population, breeders, false );
		}

		private static void CrossOver_FalloffNormal(
			EnvironmentContext env,
			List<ProjectSchedule> population,
			List<ProjectSchedule> breeders )
		{
			CrossOver_Falloff( env, population, breeders, true );
		}

		private static void CrossOver_Falloff(
			EnvironmentContext env,
			List<ProjectSchedule> population,
			List<ProjectSchedule> breeders,
			bool normalDistr )
		{
			for ( int i = 0; i < breeders.Count; ++i ) {
				for ( int j = i + 1; j < breeders.Count; ++j ) {
					double c = normalDistr ? env.Random.NextNormal() : env.Random.NextDouble();
					double p = ( breeders.Count - j ) / ( (double)breeders.Count - i );

					if ( c < p ) {
						population.AddRange( breeders.ElementAt( i ).CrossOver(
							env, breeders.ElementAt( j ) ) );
					}
				}
			}
		}

		#endregion

		private static Dictionary<ProjectSchedule, double> ComputeFitness(
			EnvironmentContext env, List<ProjectSchedule> population )
		{
			Dictionary<ProjectSchedule, double> fitnessMap = new Dictionary<ProjectSchedule, double>();
			population.ForEach( e => fitnessMap.Add( e, e.GetFitness( env ) ) );
			return fitnessMap;
		}

		private static void UpdateLogs(
			EnvironmentContext env,
			List<ProjectSchedule> population,
			int generation,
			Dictionary<int, double> min,
			Dictionary<int, double> max,
			Dictionary<int, double> avg )
		{
			if ( min.ContainsKey( generation ) ) {
				min.Remove( generation );
				max.Remove( generation );
				avg.Remove( generation );
			}

			Dictionary<ProjectSchedule, double> fm = ComputeFitness( env, population );
			min.Add( generation, fm.Min( pair => pair.Value ) );
			max.Add( generation, fm.Max( pair => pair.Value ) );
			avg.Add( generation, fm.Values.Average() );
		}

		private static void Assert( bool condition )
		{
			if ( !condition )
				throw new Exception( "Assertion failed!" );
		}

		private static void DumpLogs(
			string path,
			Dictionary<int, double> min,
			Dictionary<int, double> max,
			Dictionary<int, double> avg )
		{
			StringBuilder buf = new StringBuilder();

			buf.AppendFormat( "{0,14}\t{1,14}\t{2,14}\n", "Min", "Max", "Avg" );
			foreach ( int key in min.Keys ) {
				buf.AppendFormat( CultureInfo.InvariantCulture, "{0,14:F10}\t{1,14:F10}\t{2,14:F10}\n", min[key], max[key], avg[key] );
			}

			File.WriteAllText( path, buf.ToString() );
		}

		private static void DumpParams( string path, EnvironmentContext env )
		{
			StringBuilder buf = new StringBuilder();

			buf.AppendFormat( "Population: {0}\n", _populationSize );
			buf.AppendFormat( "Generations: {0}\n", _generationLimit );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Breeder fraction: {0}\n", _breederFraction );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Clone threshold: {0}\n", _cloneThreshold );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Mutation chance: {0}\n", env.ProbabilityMutation );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Crossover chance: {0}\n", env.ProbabilityOffspring );
			buf.AppendFormat( "Algorithmic prereq: {0}\n", env.AlgorithmicPrerequisites );
			buf.AppendFormat( "Clone elimination: {0}\n", _cloneElimination );

			File.WriteAllText( path, buf.ToString() );
		}

		private class FitnessComparer : IEqualityComparer<ProjectSchedule>
		{
			private EnvironmentContext env;

			public FitnessComparer( EnvironmentContext env )
			{
				this.env = env;
			}

			public bool Equals( ProjectSchedule a, ProjectSchedule b )
			{
				return a.GetFitness( env ) == b.GetFitness( env );
			}

			public int GetHashCode( ProjectSchedule ps )
			{
				return ps.GetHashCode();
			}
		}
	}
}
