using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectScheduling.Model
{
	public class ProjectSchedule : IEquatable<ProjectSchedule>
	{
		private bool recomputeFitness = true;
		private double cachedFitness;

		public List<int> Genotype { get; private set; }

		private ProjectSchedule()
		{
			Genotype = new List<int>();
		}

		private ProjectSchedule( ProjectSchedule source )
		{
			Genotype = new List<int>( source.Genotype.Count );
			foreach ( int gene in source.Genotype )
				Genotype.Add( gene );
		}

		public ProjectSchedule( EnvironmentContext env )
			: this()
		{
			Genotype.Capacity = env.Tasks.Count * 2;
			for ( int i = 0; i < env.Tasks.Count; ++i ) {
				Genotype.Add( env.RandomResourceId( i ) );
			}
			for ( int i = 0; i < env.Tasks.Count; ++i ) {
				Genotype.Add( env.RandomPriority() );
			}
		}

		public ProjectSchedule DeepCopy()
		{
			return new ProjectSchedule( this );
		}

		public double GetFitness( EnvironmentContext env )
		{
			if ( recomputeFitness ) {
				cachedFitness = env.Evaluate( this );
				recomputeFitness = false;
			}
			return cachedFitness;
		}

		public void Mutate( EnvironmentContext env, double overrideMutationChance = -1, bool isClone = false )
		{
			if ( overrideMutationChance < 0 )
				overrideMutationChance = env.ProbabilityMutation;

			for ( int i = 0; i < env.Tasks.Count; ++i ) {
				if ( env.Random.NextDouble() < overrideMutationChance ) {
					int old = Genotype[i];
					Genotype[i] = env.RandomResourceId( i );
					recomputeFitness |= old != Genotype[i];
				}
			}

			int offset = env.Tasks.Count;
			for ( int i = 0; i < env.Tasks.Count; ++i ) {
				if ( env.Random.NextDouble() < overrideMutationChance ) {
					int j = i + offset;
					int old = Genotype[j];
					Genotype[j] = env.RandomPriority();
					recomputeFitness |= old != Genotype[j];
				}
			}
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
					Genotype.Add( parentA.Genotype[i] );
				else
					Genotype.Add( parentB.Genotype[i] );
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

			for ( int i = 0; i < Genotype.Count; ++i ) {
				hash = hash * 31 + Genotype[i].GetHashCode();
			}

			return hash;
		}
	}
}
