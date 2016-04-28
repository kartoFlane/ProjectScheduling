using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectScheduling.Model
{
	public class ProjectSchedule : IEquatable<ProjectSchedule>
	{
		private bool recomputeFitness = true;
		private double cachedFitness;

		public List<TaskData> Genotype { get; private set; }

		private ProjectSchedule()
		{
			Genotype = new List<TaskData>();
		}

		public ProjectSchedule( EnvironmentContext env )
			: this()
		{
			for ( int i = 0; i < env.Tasks.Count; ++i ) {
				Genotype.Add( new TaskData( i, env.RandomResourceId( i ), env.RandomPriority() ) );
			}
		}

		public ProjectSchedule DeepCopy()
		{
			ProjectSchedule result = new ProjectSchedule();
			Genotype.ForEach( e => result.Genotype.Add( e.Copy() ) );
			return result;
		}

		public double GetFitness( EnvironmentContext env )
		{
			if ( recomputeFitness ) {
				cachedFitness = env.Evaluate( this );
				recomputeFitness = false;
			}
			return cachedFitness;
		}

		public void Mutate( EnvironmentContext env, double overrideMutationChance = -1 )
		{
			if ( overrideMutationChance < 0 )
				overrideMutationChance = env.ProbabilityMutation;

			Genotype.ForEach( gene =>
			{
				recomputeFitness |= gene.Mutate( env, overrideMutationChance );
			} );
		}

		public ProjectSchedule[] CrossOver( EnvironmentContext env, ProjectSchedule fiancee )
		{
			if ( env.Random.NextDouble() < env.ProbabilityOffspring ) {
				ProjectSchedule[] result = new ProjectSchedule[1];
				result[0] = new ProjectSchedule();

				result[0].CrossOver( this, fiancee, env.Random.Next( 1, Genotype.Count - 1 ) );

				return result;
			}
			else {
				return new ProjectSchedule[0];
			}
		}

		private void CrossOver( ProjectSchedule parentA, ProjectSchedule parentB, int start )
		{
			for ( int i = 0; i < parentA.Genotype.Count; ++i ) {
				if ( i <= start )
					Genotype.Add( parentA.Genotype[i].Copy() );
				else
					Genotype.Add( parentB.Genotype[i].Copy() );
			}
			recomputeFitness = true;
		}

		public override string ToString()
		{
			StringBuilder buf = new StringBuilder();

			buf.Append( "{ " );

			for ( int i = 0; i < Genotype.Count; ++i ) {
				buf.Append( Genotype[i] );

				if ( i != Genotype.Count - 1 )
					buf.Append( ", " );
			}

			buf.Append( " }" );

			return buf.ToString();
		}

		public bool Equals( ProjectSchedule other )
		{
			bool result = true;

			for ( int i = 0; i < Genotype.Count && result; ++i ) {
				result &= Genotype[i].Equals( other.Genotype[i] );
			}

			return result;
		}

		public override bool Equals( object obj )
		{
			if ( obj is ProjectSchedule )
				return Equals( (ProjectSchedule)obj );
			return false;
		}

		public override int GetHashCode()
		{
			int hash = 19;
			foreach ( TaskData td in Genotype ) {
				hash = hash * 31 + td.GetHashCode();
			}
			//return hash;
			return 0;
		}
	}
}
