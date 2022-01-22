﻿
using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Ballers
{

	public enum GravityType
	{
		Default,
		Magnet,
		Manipulated
	}

	public partial class Ball : Player
	{
		public const float Acceleration = 700f; // yeah
		public const float AirControl = 0.85f; // acceleration multiplier in air
		public const float MaxSpeed = 1100f; // this is the max speed the ball can accelerate to by itself

		public const float Friction = 0.1f;//0.15f; // resistance multiplier on ground
		public const float Viscosity = 3f; // resistance multiplier in water
		public const float Drag = 0.05f; // resistance multiplier in air
		public const float Bounciness = .35f; // elasticity of collisions, aka how much boing 
		public const float Buoyancy = 2.5f; // floatiness

		public const float Mass = 50f; // how heavy!!

		public Vector3 GetGravity()
		{
			if ( GravityType == GravityType.Default )
				return PhysicsWorld.Gravity;
			else
				return GravityDirection * PhysicsWorld.Gravity.Length;
		}

		[Net, Predicted] public GravityType GravityType { get; private set; }
		[Net, Predicted] public Vector3 GravityDirection { get; private set; }
		[Net, Predicted] public bool Grounded { get; private set; }

		private void SimulatePhysics()
		{
			Rotation gravityRotation = Rotation.LookAt( GetGravity().Normal ) * Rotation.FromPitch( -90f );

			Vector3 flatVelocity = Velocity - GetGravity().Normal * Velocity.Dot( GetGravity().Normal );
			Vector3 clampedVelocity = flatVelocity.ClampLength( MaxSpeed );
			float directionSpeed = clampedVelocity.Dot( MoveDirection );

			float acceleration = Acceleration;
			if ( !Grounded )
				acceleration *= AirControl;

			float t = 1f - directionSpeed / MaxSpeed;
			acceleration *= t;

			Velocity += MoveDirection * acceleration * Time.Delta;
			/*
			Vector3 clampedVelocity = Velocity.WithZ( 0 ).ClampLength( MaxSpeed );
			float directionSpeed = clampedVelocity.Dot( MoveDirection );

			float acceleration = Acceleration;
			if ( !Grounded )
				acceleration *= AirControl;

			float t = 1f - directionSpeed / MaxSpeed;
			acceleration *= t;

			Velocity += MoveDirection * acceleration * Time.Delta;
			*/


			Move();
		}

		private void TraceTriggers( out bool fallDamage )
		{
			fallDamage = false;

			TraceResult[] triggerTraces = Trace.Ray( Position, Position )
				.Radius( 40f )
				.HitLayer( CollisionLayer.Trigger, true )
				.RunAll();

			if ( triggerTraces == null )
				return;

			foreach ( var trace in triggerTraces )
			{
				if ( trace.Entity.IsValid() )
				{
					switch ( trace.Entity )
					{
						case FallDamageBrush:
							fallDamage = true;
							break;
						case HurtBrush:
							Pop();
							break;
						case CheckpointBrush checkPoint:
							if ( checkPoint.Index == CheckpointIndex + 1 )
							{
								if ( IsClient )
									Sound.FromScreen( CheckpointBrush.Swoosh.Name );
								CheckpointIndex++;
							}
							break;
						default:
							continue;
					}
				}
			}
		}

		private void Move()
		{
			TraceTriggers( out bool fallDamage );

			float dt = Time.Delta;

			var mover = new MoveHelper( Position, Velocity, this );

			Grounded = mover.TraceDirection( GetGravity().Normal ).Hit;

			TraceResult groundTrace = mover.TraceDirection( GetGravity().Normal * 16f );
			if ( groundTrace.Hit )
			{
				DebugOverlay.Sphere( groundTrace.EndPos - groundTrace.Normal * 40f, 2f, Color.White, true, 0.1f );

				string surface = groundTrace.Surface.Name;

				if ( IsClient )
					Log.Error( surface );

				switch ( surface )
				{
					case "magnet":
						GravityType = GravityType.Magnet;
						GravityDirection = -groundTrace.Normal;
						break;
					case "gravity":
						GravityType = GravityType.Manipulated;
						GravityDirection = -groundTrace.Normal;
						break;
					default:
						if ( GravityType != GravityType.Manipulated )
							GravityType = GravityType.Default;
						break;
				}
			}
			else if ( GravityType == GravityType.Magnet )
				GravityType = GravityType.Default;

			TraceResult waterTrace = Trace.Ray( Position + Vector3.Up * 80f, Position )
				.Radius( 40f )
				.HitLayer( CollisionLayer.All, false )
				.HitLayer( CollisionLayer.Water, true )
				.Run();

			float friction = Grounded ? Friction : Drag;

			if ( waterTrace.Hit )
			{
				float waterLevel = (waterTrace.EndPos.z - Position.z) * 0.0125f;
				float underwaterVolume = 0.5f - 0.5f * MathF.Cos( MathF.PI * waterLevel );
				mover.Velocity -= PhysicsWorld.Gravity * underwaterVolume * Buoyancy * dt;

				friction = Viscosity * underwaterVolume + friction * (1f - underwaterVolume);
			}

			mover.ApplyFriction( friction, dt );

			if ( ConsoleSystem.GetValue( "sv_cheats" ) == "1" && Input.Down( InputButton.Jump ) )
				mover.Velocity -= GetGravity() * dt;
			else
				mover.Velocity += GetGravity() * dt;

			mover.TryMove( dt );
			mover.TryUnstuck(); // apparently this isnt needed i think

			TraceResult moveTrace = mover.Trace
				.FromTo( mover.Position, mover.Position + mover.Velocity * dt )
				.Run();

			if ( moveTrace.Hit )
			{
				float hitForce = mover.Velocity.Dot( -moveTrace.Normal );
				PlayImpactSound( hitForce );
			}

			if ( fallDamage && (waterTrace.Hit || moveTrace.Hit) )
			{
				Pop();
				return;
			}

			Velocity = mover.Velocity;
			Position = mover.Position;

			UpdateModel();
		}

		public void PlayImpactSound( float force )
		{
			if ( IsServer )
				ClientImpactSound( this, force );
			else if ( Local.Client == Owner.Client )
				ImpactSound( force );
		}

		private void ImpactSound( float force )
		{
			if ( force > 150f )
			{
				float scale = (force - 150f) / 1000f;
				float volume = (scale * 1.2f).Clamp( 0f, 1f );
				float pitch = (scale * 3f).Clamp( 0.8f, 0.85f );

				Sound impactSound = PlaySound( BounceSound.Name );
				impactSound.SetVolume( volume );
				impactSound.SetPitch( pitch );
			}
		}

		[ClientRpc]
		public static void ClientImpactSound( Ball ball, float force )
		{
			if ( ball.Client != Local.Client || ball.Controller == ControlType.Replay )
				ball.ImpactSound( force );
		}

		public static readonly SoundEvent BounceSound = new()
		{
			Sounds = new List<string> {
			"sounds/ball/bounce1.vsnd",
			"sounds/ball/bounce2.vsnd",
			"sounds/ball/bounce3.vsnd",
			},
			Pitch = 1f,
			PitchRandom = 0.1f,
			Volume = 1f,
			DistanceMax = 2048f,
		};
	}

	public static class TraceExtensions
	{
		public static Trace Only( this Trace trace, Entity entity )
		{
			if ( entity.IsValid() )
			{
				string idTag = $"ID:{entity.NetworkIdent}";
				if ( !entity.Tags.Has( idTag ) )
					entity.Tags.Add( idTag );

				// only hit specified entity
				return trace.EntitiesOnly().WithTag( idTag );
			}

			// hit no entities if specified entity is invalid
			return trace.WithTag( "" );
		}
	}

}
