using ProjectScheduling.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;


namespace ProjectScheduling
{
	public class GeneticAlgorithm
	{
		public event Action OnAllTimeBestChanged;

		// Initial parameters
		private const int _populationSize = 30;
		private const double _breederFraction = 1;
		private const double _cloneThreshold = 0.15;
		private const double _mutationChance = 0.04;
		private const double _crossoverChance = 0.66;

		private const bool _debugOutput = true;
		private const bool _algorithmicPrerequisites = true;
		private const ECloneEliminationStrategy _cloneStrat = ECloneEliminationStrategy.MUTATION;

		// Stop conditions
		private const int _generationLimit = 100;
		private const double _fitnessThreshold = 0;

		private EnvironmentContext env;
		private FileInfo defFile;
		private ProjectSchedule allTimeBest;
		private Dictionary<int, double> minMap;
		private Dictionary<int, double> avgMap;
		private Dictionary<int, double> maxMap;

		public int GenerationLimit { get; set; }
		public int PopulationSize { get; set; }

		public double AllTimeBestFitness { get { return allTimeBest == null ? 0 : allTimeBest.GetFitness( env ); } }

		public bool RequestTerminate { get; set; }

		public static void Main()
		{
			GeneticAlgorithm ga = new GeneticAlgorithm();

			ga.Start();
		}

		public GeneticAlgorithm()
		{
			GenerationLimit = _generationLimit;
			PopulationSize = _populationSize;
		}

		public void Start()
		{
			Console.WriteLine( "Setting up environment..." );
			// Setup the environment for the genetic algorithm
			env = new EnvironmentContext();

			ECloneEliminationStrategy cloneStrat = _cloneStrat;

			//env.TimeWeight = 0.7;
			env.ProbabilityMutation = _mutationChance;
			env.ProbabilityOffspring = _crossoverChance;

			env.AlgorithmicPrerequisites = _algorithmicPrerequisites;
			env.PenaltyPrerequisite = 0.3;
			env.PenaltyIdleResource = 0.005;
			env.PenaltyWaitingTask = 0.011;

			string[] files = Directory.GetFiles( "../../../_defs/", "*.def" );
			Console.WriteLine( string.Format( "Found {0} .def files: ", files.Length ) );
			for ( int i = 0; i < files.Length; ++i ) {
				Console.WriteLine( string.Format( "{0,2}.\t{1}", i, new FileInfo( files[i] ).Name ) );
			}
			Console.WriteLine();

			// Research: 0, 1, 2, 9, 10
			// 1 - difficult def
			int fileId = 0;

			if ( fileId == -1 ) {
				string input = Console.ReadLine();
				fileId = int.Parse( input );
			}

			defFile = new FileInfo( files[fileId] );
			Console.WriteLine( "Using " + defFile.Name );
			DefIO.ReadDEF( env, defFile.FullName );

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
			minMap = new Dictionary<int, double>();
			maxMap = new Dictionary<int, double>();
			avgMap = new Dictionary<int, double>();

			Console.Write( "Generating initial population..." );
			List<ProjectSchedule> population = env.GeneratePopulation( PopulationSize );
			Console.WriteLine( " Done." );

			UpdateLogs( population, 0, minMap, maxMap, avgMap );

			allTimeBest = null;
			double startMin = minMap.Min( e => e.Value );
			double startMax = maxMap.Max( e => e.Value );

			Console.WriteLine();
			Console.WriteLine( "Started with --> Best: {0:F6} | Worst: {1:F6}", startMin, startMax );
			Console.WriteLine();

			int generationIndex = 0;
			int timeStart = Environment.TickCount;

			while ( !RequestTerminate && minMap[generationIndex] > _fitnessThreshold && generationIndex < GenerationLimit ) {
				int genTimeStart = Environment.TickCount;

				++generationIndex;
				Console.Write( "Generation: {0,4}", generationIndex );

				// Crossover
				var q = population.Take( (int)( _breederFraction * PopulationSize ) ).ToList();

				Console.Write( "\t\tCO... " );
				//CrossOver_FalloffNormal( env, population, q );
				//CrossOver_FalloffLinear( env, population, q );
				//CrossOver_SimplePairs( env, population, q );
				CrossOver_EqualOpportunity( env, population, q, true );
				Console.Write( "Done, delta: {0,3}", population.Count - PopulationSize );

				Console.Write( " | Mut... " );
				// Mutation
				population.ForEach( e => e.Mutate( env ) );
				Console.Write( "Done" );

				// Count clones
				Console.Write( "\t\tClones... " );
				double cc = population.Count - population.Distinct( comparer ).Count();
				Console.Write( "Done, #: " + cc );

				if ( cc / PopulationSize > _cloneThreshold ) {
					var c = population.GroupBy( spec => spec.GetFitness( env ) );

					Console.Write( "\t\tEliminating... " );
					switch ( cloneStrat ) {
						case ECloneEliminationStrategy.MUTATION: {
							foreach ( var g in c ) {
								foreach ( ProjectSchedule clone in g.Skip( 3 ) ) {
									clone.Mutate( env, 10 * env.ProbabilityMutation, true );
								}
							}
						} break;

						case ECloneEliminationStrategy.ELIMINATION: {
							c = c.OrderByDescending( g => g.Key );
							foreach ( var g in c ) {
								foreach ( ProjectSchedule clone in g.Skip( 3 ) ) {
									population.Remove( clone );
								}
							}

							while ( population.Count < PopulationSize ) {
								population.Add( new ProjectSchedule( env ) );
							}
						} break;

						default: {
							throw new NotImplementedException( cloneStrat.ToString() );
						}
					}
					Console.Write( "Done" );
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
				UpdateLogs( population, generationIndex, minMap, maxMap, avgMap );

				Console.Write( "\t\tMIN: {0:F6}", minMap[generationIndex] );
				Console.Write( "\t\tAVG: {0:F6}", avgMap[generationIndex] );
				Console.Write( "\t\tMAX: {0:F6}", maxMap[generationIndex] );

				// Find all-time best
				bool changed = false;
				foreach ( ProjectSchedule specimen in population ) {
					if ( allTimeBest == null ||
							specimen.GetFitness( env ) < allTimeBest.GetFitness( env ) ) {
						allTimeBest = specimen;
						changed = true;
					}
				}

				if ( changed ) {
					allTimeBest = allTimeBest.DeepCopy();
					if ( OnAllTimeBestChanged != null ) {
						OnAllTimeBestChanged();
					}
				}

				if ( population.Contains( allTimeBest, comparer ) ) {
					population.Add( allTimeBest.DeepCopy() );
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

			DumpAllData();
		}

		public void DumpAllData()
		{
			File.Copy( defFile.FullName, "../../../_solutions/result.def", true );
			DefIO.WriteDEF( env, allTimeBest, "../../../_solutions/result.sol", _debugOutput );
			DumpLogs( "../../../_solutions/dump.txt", minMap, maxMap, avgMap );
			DumpParams( "../../../_solutions/params.txt", defFile.Name );
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
			List<ProjectSchedule> breeders,
			bool doubleCross )
		{
			for ( int i = 0; i < breeders.Count; ++i ) {
				int j = doubleCross ? 0 : i + 1;
				for ( ; j < breeders.Count; ++j ) {
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

		private Dictionary<ProjectSchedule, double> ComputeFitness( List<ProjectSchedule> population )
		{
			Dictionary<ProjectSchedule, double> fitnessMap = new Dictionary<ProjectSchedule, double>();
			population.ForEach( e => fitnessMap.Add( e, e.GetFitness( env ) ) );
			return fitnessMap;
		}

		private void UpdateLogs(
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

			Dictionary<ProjectSchedule, double> fm = ComputeFitness( population );
			min.Add( generation, fm.Min( pair => pair.Value ) );
			max.Add( generation, fm.Max( pair => pair.Value ) );
			avg.Add( generation, fm.Values.Average() );
		}

		private static void Assert( bool condition )
		{
			if ( !condition )
				throw new Exception( "Assertion failed!" );
		}

		private void DumpLogs(
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

		private void DumpParams( string path, string defFileName )
		{
			StringBuilder buf = new StringBuilder();

			buf.AppendFormat( "File: {0}\n", defFileName );
			buf.AppendFormat( "Population: {0}\n", PopulationSize );
			buf.AppendFormat( "Generations: {0}\n", GenerationLimit );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Breeder fraction: {0}\n", _breederFraction );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Clone threshold: {0}\n", _cloneThreshold );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Mutation chance: {0}\n", env.ProbabilityMutation );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Crossover chance: {0}\n", env.ProbabilityOffspring );
			buf.AppendFormat( "Algorithmic prereq: {0}\n", env.AlgorithmicPrerequisites );
			buf.AppendFormat( "Clone elim. strategy: {0}\n", _cloneStrat.ToString() );

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
				return ps.GetFitness( env ).GetHashCode();
			}
		}

		private enum ECloneEliminationStrategy
		{
			ELIMINATION, MUTATION
		}
	}
}
