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

		private EnvironmentContext env;

		private FileInfo defFile;
		private DirectoryInfo outputDir;

		private ProjectSchedule allTimeBest;

		private Dictionary<int, double> minMap;
		private Dictionary<int, double> avgMap;
		private Dictionary<int, double> maxMap;

		#region Publicly modifiable properties

		public int GenerationLimit { get; set; }
		public int PopulationSize { get; set; }
		public int TournamentSize { get; set; }
		public double BreederFraction { get; set; }
		public double CloneThreshold { get; set; }
		public double MutationChance { get; set; }
		public double CrossoverChance { get; set; }
		public ECloneEliminationStrategy CloneElimination { get; set; }
		public ECrossoverStrategy CrossoverStrategy { get; set; }
		public ESelectionStrategy SelectionStrategy { get; set; }

		public double PenaltyRelations { get; set; }
		public double PenaltyIdleResource { get; set; }
		public double PenaltyWaitingTask { get; set; }

		public string InputDef { get; set; }
		public string OutputDir { get; set; }

		public bool DebugOutput { get; set; }
		public bool AlgorithmicRelations { get; set; }

		#endregion

		public double AllTimeBestFitness { get { return allTimeBest == null ? 0 : allTimeBest.GetFitness( env ); } }

		public bool RequestTerminate { get; set; }

		public static void Main()
		{
			GeneticAlgorithm ga = new GeneticAlgorithm();

			string[] files = Directory.GetFiles( "../../../_defs/", "*.def" );
			Console.WriteLine( string.Format( "Found {0} .def files: ", files.Length ) );
			for ( int i = 0; i < files.Length; ++i ) {
				Console.WriteLine( string.Format( "{0,2}.\t{1}", i, new FileInfo( files[i] ).Name ) );
			}
			Console.WriteLine();

			// 0, 1, 2, 3, 4
			int fileId = 1;

			ga.InputDef = files[fileId];

			ga.Start();
		}

		public GeneticAlgorithm()
		{
			GenerationLimit = 500;
			PopulationSize = 300;
			TournamentSize = 10;
			BreederFraction = 1;
			CloneThreshold = 0.15;
			MutationChance = 0.015;
			CrossoverChance = 1.00;
			CloneElimination = ECloneEliminationStrategy.MUTATION;
			CrossoverStrategy = ECrossoverStrategy.SIMPLE_PAIRS;
			SelectionStrategy = ESelectionStrategy.RANKING;

			DebugOutput = true;
			AlgorithmicRelations = true;

			InputDef = "../../../_defs/100_10_48_15.def";
			OutputDir = "../../../_solutions/";

			PenaltyRelations = 0.3;
			PenaltyIdleResource = 0.3;
			PenaltyWaitingTask = 0.1;
		}

		public GeneticAlgorithm( Parameters parameters )
		{
			GenerationLimit = parameters.GenerationLimit;
			PopulationSize = parameters.PopulationSize;
			TournamentSize = parameters.TournamentSize;
			BreederFraction = parameters.BreederFraction;
			CloneThreshold = parameters.CloneThreshold;
			MutationChance = parameters.MutationChance;
			CrossoverChance = parameters.CrossoverChance;
			CloneElimination = parameters.CloneElimination;
			CrossoverStrategy = parameters.CrossoverStrategy;
			SelectionStrategy = parameters.SelectionStrategy;

			DebugOutput = parameters.DebugOutput;
			AlgorithmicRelations = parameters.AlgorithmicRelations;

			InputDef = parameters.InputDef;
			OutputDir = parameters.OutputDir;

			PenaltyRelations = parameters.PenaltyRelations;
			PenaltyIdleResource = parameters.PenaltyIdleResource;
			PenaltyWaitingTask = parameters.PenaltyWaitingTask;
		}

		public void Start()
		{
			Console.WriteLine( "Setting up environment..." );
			// Setup the environment for the genetic algorithm
			env = new EnvironmentContext();

			env.ProbabilityMutation = MutationChance;
			env.ProbabilityOffspring = CrossoverChance;

			env.AlgorithmicRelations = AlgorithmicRelations;
			env.PenaltyRelations = PenaltyRelations;
			env.PenaltyIdleResource = PenaltyIdleResource;
			env.PenaltyWaitingTask = PenaltyWaitingTask;

			defFile = new FileInfo( InputDef );

			if ( !defFile.Exists )
				throw new ArgumentException( "File doesn't exist: " + InputDef );

			if ( Directory.Exists( OutputDir ) ) {
				outputDir = new DirectoryInfo( OutputDir );
			}
			else {
				outputDir = Directory.CreateDirectory( OutputDir );
			}

			Console.WriteLine( "Using " + defFile.Name );
			DefIO.ReadDEF( env, defFile.FullName );

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

			while ( !RequestTerminate && generationIndex < GenerationLimit ) {
				int genTimeStart = Environment.TickCount;

				++generationIndex;
				Console.Write( "Generation: {0,4}", generationIndex );

				// Crossover
				var q = population.Take( (int)( BreederFraction * PopulationSize ) ).ToList();

				Console.Write( "\t\tCO... " );
				switch ( CrossoverStrategy ) {
					case ECrossoverStrategy.SIMPLE_PAIRS: {
						CrossOver_SimplePairs( env, population, q );
					} break;
					case ECrossoverStrategy.FALLOFF_LINEAR: {
						CrossOver_FalloffLinear( env, population, q );
					} break;
					case ECrossoverStrategy.FALLOFF_COSINE: {
						CrossOver_FalloffCosine( env, population, q );
					} break;
					case ECrossoverStrategy.EQUAL_OPPORTUNITY: {
						CrossOver_EqualOpportunity( env, population, q, false );
					} break;
					case ECrossoverStrategy.ORGY: {
						CrossOver_EqualOpportunity( env, population, q, true );
					} break;
					default: {
						throw new NotImplementedException( CrossoverStrategy.ToString() );
					}
				}
				Console.Write( "Done, delta: {0,3}", population.Count - PopulationSize );

				Console.Write( " | Mut... " );
				// Mutation
				population.ForEach( e => e.Mutate( env ) );
				Console.Write( "Done" );

				// Count clones
				Console.Write( "\t\tClones... " );
				double cc = population.Count - population.Distinct().Count();
				Console.Write( "Done, #: " + cc );

				if ( cc / PopulationSize > CloneThreshold ) {
					var c = population.GroupBy( spec => spec );

					Console.Write( "\t\tEliminating... " );
					switch ( CloneElimination ) {
						case ECloneEliminationStrategy.MUTATION: {
							foreach ( var g in c ) {
								foreach ( ProjectSchedule clone in g.Skip( 1 ) ) {
									clone.Mutate( env, 10 * env.ProbabilityMutation, true );
								}
							}
						} break;

						case ECloneEliminationStrategy.ELIMINATION: {
							c = c.OrderByDescending( g => g.Key );
							foreach ( var g in c ) {
								foreach ( ProjectSchedule clone in g.Skip( 1 ) ) {
									population.Remove( clone );
								}
							}

							while ( population.Count < PopulationSize ) {
								population.Add( new ProjectSchedule( env ) );
							}
						} break;

						default: {
							throw new NotImplementedException( CloneElimination.ToString() );
						}
					}
					Console.Write( "Done" );
				}

				// Selection
				Console.Write( "\t\tSelection... " );
				switch ( SelectionStrategy ) {
					case ESelectionStrategy.RANKING: {
						// Find top (populationCount) specimens in order to keep the population stable
						population = population
							.OrderBy( e => e.GetFitness( env ) )
							.Take( PopulationSize )
							.ToList();
					} break;

					case ESelectionStrategy.TOURNAMENT: {
						List<ProjectSchedule> npop = new List<ProjectSchedule>( PopulationSize );
						while ( npop.Count < PopulationSize ) {
							List<ProjectSchedule> t = new List<ProjectSchedule>();
							for ( int i = 0; i < TournamentSize; ++i ) {
								t.Add( env.Random.From( population ) );
							}

							t.OrderBy( spec => spec.GetFitness( env ) );
							npop.Add( t[0] );
						}
						population = npop;
					} break;

					default: {
						throw new NotImplementedException( SelectionStrategy.ToString() );
					}
				}
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

				if ( !population.Contains( allTimeBest ) ) {
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
			File.Copy( defFile.FullName, outputDir + "result.def", true );
			DefIO.WriteDEF( env, allTimeBest, outputDir + "result.sol", DebugOutput );
			DumpLogs( outputDir + "dump.txt", minMap, maxMap, avgMap );
			DumpParams( outputDir + "params.txt", defFile.Name );
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
			for ( int i = 0; i < breeders.Count; ++i ) {
				for ( int j = i + 1; j < breeders.Count; ++j ) {
					double p = ( breeders.Count - j - 1 ) / ( (double)breeders.Count - i );

					if ( env.Random.NextDouble() < p ) {
						population.AddRange( breeders.ElementAt( i ).CrossOver(
							env, breeders.ElementAt( j ) ) );
					}
				}
			}
		}

		private static void CrossOver_FalloffCosine(
			EnvironmentContext env,
			List<ProjectSchedule> population,
			List<ProjectSchedule> breeders )
		{
			for ( int i = 0; i < breeders.Count; ++i ) {
				for ( int j = i + 1; j < breeders.Count; ++j ) {
					double f = ( breeders.Count - j - 1 ) / ( (double)breeders.Count - i );
					double p = Math.Cos( f * Math.PI * 0.5 );

					if ( env.Random.NextDouble() < p ) {
						population.AddRange( breeders.ElementAt( i ).CrossOver(
							env, breeders.ElementAt( j ) ) );
					}
				}
			}
		}

		#endregion

		private List<double> ComputeFitness( List<ProjectSchedule> population )
		{
			List<double> result = new List<double>();
			foreach ( ProjectSchedule specimen in population ) {
				result.Add( specimen.GetFitness( env ) );
			}
			return result;
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

			List<double> fitList = ComputeFitness( population );
			min.Add( generation, fitList.Min() );
			max.Add( generation, fitList.Max() );
			avg.Add( generation, fitList.Average() );
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
			buf.AppendFormat( CultureInfo.InvariantCulture, "Breeder fraction: {0}\n", BreederFraction );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Clone threshold: {0}\n", CloneThreshold );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Mutation chance: {0}\n", MutationChance );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Crossover chance: {0}\n", CrossoverChance );
			buf.AppendFormat( "Algorithmic prereq: {0}\n", AlgorithmicRelations );
			buf.AppendFormat( "Clone elim. strategy: {0}\n", CloneElimination.ToString() );
			buf.AppendFormat( "Crossover strategy: {0}\n", CrossoverStrategy.ToString() );
			buf.AppendFormat( "Penalty Idle Resource: {0}\n", PenaltyIdleResource );
			buf.AppendFormat( "Penalty Waiting Task: {0}\n", PenaltyWaitingTask );

			File.WriteAllText( path, buf.ToString() );
		}

		public class Parameters
		{
			public int GenerationLimit { get; set; }
			public int PopulationSize { get; set; }
			public int TournamentSize { get; set; }
			public double BreederFraction { get; set; }
			public double CloneThreshold { get; set; }
			public double MutationChance { get; set; }
			public double CrossoverChance { get; set; }
			public ECloneEliminationStrategy CloneElimination { get; set; }
			public ECrossoverStrategy CrossoverStrategy { get; set; }
			public ESelectionStrategy SelectionStrategy { get; set; }

			public double PenaltyRelations { get; set; }
			public double PenaltyIdleResource { get; set; }
			public double PenaltyWaitingTask { get; set; }

			public string InputDef { get; set; }
			public string OutputDir { get; set; }

			public bool DebugOutput { get; set; }
			public bool AlgorithmicRelations { get; set; }

			public Parameters()
			{
			}

			public Parameters( Parameters other )
			{
				GenerationLimit = other.GenerationLimit;
				PopulationSize = other.PopulationSize;
				TournamentSize = other.TournamentSize;
				BreederFraction = other.BreederFraction;
				CloneThreshold = other.CloneThreshold;
				MutationChance = other.MutationChance;
				CrossoverChance = other.CrossoverChance;
				CloneElimination = other.CloneElimination;
				CrossoverStrategy = other.CrossoverStrategy;
				SelectionStrategy = other.SelectionStrategy;

				DebugOutput = other.DebugOutput;
				AlgorithmicRelations = other.AlgorithmicRelations;

				InputDef = other.InputDef;
				OutputDir = other.OutputDir;

				PenaltyRelations = other.PenaltyRelations;
				PenaltyIdleResource = other.PenaltyIdleResource;
				PenaltyWaitingTask = other.PenaltyWaitingTask;
			}
		}
	}

	public enum ECloneEliminationStrategy
	{
		ELIMINATION, MUTATION
	}

	public enum ECrossoverStrategy
	{
		SIMPLE_PAIRS,
		FALLOFF_LINEAR,
		FALLOFF_COSINE,
		EQUAL_OPPORTUNITY,
		ORGY
	}

	public enum ESelectionStrategy
	{
		RANKING,
		TOURNAMENT
	}
}
