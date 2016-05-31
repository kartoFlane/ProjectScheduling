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
		private long timeStart;

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
		public ECrossoverType CrossoverType { get; set; }

		public double PenaltyIdleResource { get; set; }
		public double PenaltyWaitingTask { get; set; }
		public double PenaltySkill { get; set; }

		public string InputDef { get; set; }
		public string OutputDir { get; set; }

		public bool DebugOutput { get; set; }

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
			GenerationLimit = 200;
			PopulationSize = 30;
			TournamentSize = 10;
			BreederFraction = 1;
			CloneThreshold = 0.15;
			MutationChance = 0.04;
			CrossoverChance = 1.00;
			CloneElimination = ECloneEliminationStrategy.MUTATION;
			CrossoverStrategy = ECrossoverStrategy.ORGY;
			SelectionStrategy = ESelectionStrategy.RANKING;
			CrossoverType = ECrossoverType.SINGLE_POINT;

			DebugOutput = true;

			InputDef = "../../../_defs/100_10_48_15.def";
			OutputDir = "../../../_solutions/";

			PenaltyIdleResource = 0.3;
			PenaltyWaitingTask = 0.1;
			PenaltySkill = 0.5;
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
			CrossoverType = parameters.CrossoverType;

			DebugOutput = parameters.DebugOutput;

			InputDef = parameters.InputDef;
			OutputDir = parameters.OutputDir;

			PenaltyIdleResource = parameters.PenaltyIdleResource;
			PenaltyWaitingTask = parameters.PenaltyWaitingTask;
			PenaltySkill = parameters.PenaltySkill;
		}

		public void Start()
		{
			Console.WriteLine( "Setting up environment..." );
			// Setup the environment for the genetic algorithm
			env = new EnvironmentContext();

			env.ProbabilityMutation = MutationChance;
			env.ProbabilityOffspring = CrossoverChance;
			env.CrossoverType = CrossoverType;

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
			timeStart = Environment.TickCount;

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
				population.AddRange( breeders.ElementAt( i + 1 ).CrossOver( env, breeders.ElementAt( i ) ) );
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

			double totalTime = ( Environment.TickCount - timeStart ) / 1000.0;

			buf.AppendFormat( "File: {0}\n", defFileName );
			buf.AppendFormat( "Population: {0}\n", PopulationSize );
			buf.AppendFormat( "Generations: {0}\n", GenerationLimit );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Total time: {0}s\n", totalTime );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Breeder fraction: {0}\n", BreederFraction );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Clone threshold: {0}\n", CloneThreshold );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Mutation chance: {0}\n", MutationChance );
			buf.AppendFormat( CultureInfo.InvariantCulture, "Crossover chance: {0}\n", CrossoverChance );
			buf.AppendFormat( "Clone elim. strategy: {0}\n", CloneElimination.ToString() );
			buf.AppendFormat( "Crossover strategy: {0}\n", CrossoverStrategy.ToString() );
			buf.AppendFormat( "Penalty Idle Resource: {0}\n", PenaltyIdleResource );
			buf.AppendFormat( "Penalty Waiting Task: {0}\n", PenaltyWaitingTask );
			buf.AppendFormat( "Crossover type: {0}\n", CrossoverType.ToString() );

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
			public ECrossoverType CrossoverType { get; set; }

			public double PenaltyIdleResource { get; set; }
			public double PenaltyWaitingTask { get; set; }
			public double PenaltySkill { get; set; }

			public string InputDef { get; set; }
			public string OutputDir { get; set; }

			public bool DebugOutput { get; set; }

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
				CrossoverType = other.CrossoverType;

				DebugOutput = other.DebugOutput;

				InputDef = other.InputDef;
				OutputDir = other.OutputDir;

				PenaltyIdleResource = other.PenaltyIdleResource;
				PenaltyWaitingTask = other.PenaltyWaitingTask;
				PenaltySkill = other.PenaltySkill;
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

	public enum ECrossoverType
	{
		SINGLE_POINT,
		DOUBLE_POINT
	}
}
