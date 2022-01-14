﻿using Sandbox;
using System.Linq;
using System;

namespace Ballers
{
	public struct MoveHelper
	{
		public Vector3 Position;
		public Vector3 Velocity;

		public Trace Trace;

		public MoveHelper( Vector3 position, Vector3 velocity ) : this()
		{
			Velocity = velocity;
			Position = position;

			// Hit everything but other balls
			Trace = Trace.Ray( 0, 0 )
				.Radius( 40f )
				.HitLayer( CollisionLayer.Solid, true )
				.HitLayer( CollisionLayer.PLAYER_CLIP, true )
				.HitLayer( CollisionLayer.GRATE, true )
				.HitLayer( CollisionLayer.STATIC_LEVEL, true )
				.HitLayer( CollisionLayer.LADDER, false )
				.WorldOnly();
		}

		public TraceResult TraceFromTo( Vector3 start, Vector3 end )
		{
			return Trace.FromTo( start, end ).Run();
		}

		public TraceResult TraceDirection( Vector3 down )
		{
			return TraceFromTo( Position, Position + down );
		}

		public float TryMove( float timestep )
		{
			float travelFraction = 0;

			using var moveplanes = new VelocityClipPlanes( Velocity );


			for ( int bump = 0; bump < moveplanes.Max; bump++ )
			{
				if ( Velocity.Length.AlmostEqual( 0.0f ) )
					break;

				/*
				foreach ( MovingBrush brush in MovingBrush.All )
				{

					Vector3 relativeVelocity = Velocity - brush.Velocity;

					Vector3 movePos = Position + relativeVelocity * timestep;

					TraceResult tr = Trace.Ray( Position, movePos )
					.Radius( 40f )
					.HitLayer( CollisionLayer.All, false )
					.HitLayer( CollisionLayer.LADDER, true )
					.Only( brush )
					.Run();

					if ( tr.Hit )
					{
						//DebugOverlay.Sphere( tr.EndPos, 40f, Color.White );
						float planeVel = brush.Velocity.Dot( tr.Normal );
	
						float normalVel = -tr.Normal.Dot( relativeVelocity );
						DebugOverlay.Text( tr.EndPos, normalVel.ToString() );

						Position += tr.Normal * 0.1f;

						if ( !moveplanes.TryAdd( tr.Normal, tr.Normal*normalVel, ref Velocity, Ball.Bounciness ) )
							break;
					}
				}
				*/

				var pm = Trace.FromTo( Position, Position + Velocity * timestep ).IgnoreMovingBrushes().Run();

				if ( pm.StartedSolid )
				{
					Position += pm.Normal * 0.01f;

					continue;
				}

				travelFraction += pm.Fraction;

				if ( pm.Fraction > 0.0f )
				{
					Position = pm.EndPos + pm.Normal * 0.01f;

					moveplanes.StartBump( Velocity );
				}

				timestep -= timestep * pm.Fraction;

				bool hitEntity = pm.Hit && pm.Entity.IsValid();

				Vector3 vel = hitEntity ? pm.Entity.Velocity : Vector3.Zero;

				if ( !moveplanes.TryAdd( pm.Normal, vel, ref Velocity, Ball.Bounciness ) )
					break;
			}

			if ( travelFraction == 0 )
				Velocity = 0;

			return travelFraction;
		}

		public void ApplyFriction( float frictionAmount, float delta )
		{
			float StopSpeed = 100.0f;

			var speed = Velocity.Length;
			if ( speed < 0.1f )
			{
				Velocity = 0;
				return;
			}

			// Bleed off some speed, but if we have less than the bleed
			//  threshold, bleed the threshold amount.
			float control = (speed < StopSpeed) ? StopSpeed : speed;

			// Add the amount to the drop amount.
			var drop = control * delta * frictionAmount;

			// scale the velocity
			float newspeed = speed - drop;
			if ( newspeed < 0 ) newspeed = 0;
			if ( newspeed == speed ) return;

			newspeed /= speed;
			Velocity *= newspeed;
		}

		public void TryUnstuck()
		{
			var tr = TraceFromTo( Position, Position );
			if ( !tr.StartedSolid ) return;

			Position += tr.Normal * 1.0f;
			Velocity += tr.Normal * 50.0f;
		}
	}
}
