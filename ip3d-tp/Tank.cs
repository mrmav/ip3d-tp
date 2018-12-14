﻿using ip3d_tp.Particles;
using ip3d_tp.Physics3D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace ip3d_tp
{
    class Tank
    {
        // game reference
        Game Game;

        // the loaded 3d model
        Model Model;

        // defines a physics body
        public Body Body;
        public Box BodyDebug;
        
        public Matrix WorldTransform
        {
            get
            {
                return Body.Bounds.WorldTransform;
            }
        }

        // an axis for debug purposes
        Axis3D Axis;

        public Vector3 Scale;

        float YawStep = MathHelper.ToRadians(180f);  // in degrees

        float TurretYaw = 0f;
        float CanonPitch = 0f;
        
        // the shader to render the tank
        Effect Shader;

        // rasterizer
        RasterizerState SolidRasterizerState;

        // blend state to render this model meshes
        BlendState BlendState;

        // array to store the Bones Transformations
        Matrix[] BoneTransforms;

        // create references to the wheels
        ModelBone LFrontWheel;
        ModelBone LBackWheel;
        ModelBone RFrontWheel;
        ModelBone RBackWheel;
        ModelBone RFrontSteer;
        ModelBone LFrontSteer;

        ModelBone Turret;
        ModelBone Canon;

        Matrix LFrontWheelTransform;
        Matrix LBackWheelTransform;
        Matrix RFrontWheelTransform;
        Matrix RBackWheelTransform;
        Matrix RFrontSteerTransform;
        Matrix LFrontSteerTransform;

        Matrix TurretTransform;
        Matrix CanonTransform;

        // current wheels angle
        float WheelsAngle = 0f;
        float SteerAngle = 0f;
        float MaxSteerAngle = MathHelper.ToRadians(10f);

        // an array containing the needed textures
        Texture2D[] Textures;

        // textures for shading enrichment
        Texture2D BurrsMap;
        Texture2D SpecularMap;
        Texture2D NormalMap;

        // this tank ID for controls
        public short TankID;

        // projectiles
        float LastShot = 0f;
        float ProjectilePower = 2.8f;
        float ShootRate = 150f;

        // bullets pool
        public List<Projectile> Bullets;

        // particle emitters for some effects
        LineParticleEmitter Particles;

        // constructor
        public Tank(Game game)
        {

            Game = game;

            // tank loaded from fbx
            Model = Game.Content.Load<Model>("Models/Tank/tank2");

            // create the physics body
            Body = new Body(0f, 0f, 0f, 5f, 5f, 7f);
            Body.Acceleration = new Vector3(0.1f);
            Body.MaxVelocity = 0.5f;
            Body.Drag = new Vector3(0.8f);

            // init values
            Body.SetPosition(Vector3.Zero);
            Body.SetRotation(Vector3.Zero);
            Body.Offset = new Vector3(0f, 2f, -0.25f);
            Body.SetSize(4.3f, 3.2f, 6.5f);
            Scale = new Vector3(1.00f);  // the importer is already scaling the model to our needed dimensions

            Body.Bounds.Yaw = MathHelper.ToRadians(90f);

            BodyDebug = new Box(Game, Body.Offset, Body.CollisionRect.Width, Body.CollisionRect.Height, Body.CollisionRect.Depth);
            BodyDebug.ShowSolid = false;
            BodyDebug.ShowWireframe = true;

            // loading the shader
            Shader = Game.Content.Load<Effect>("Effects/Tank");
            BurrsMap = Game.Content.Load<Texture2D>("Textures/metal_diffuse_1k");
            SpecularMap = Game.Content.Load<Texture2D>("Textures/metal_specular_1k");
            NormalMap = Game.Content.Load<Texture2D>("Textures/metal_normal_1k");

            BoneTransforms = new Matrix[Model.Bones.Count];

            LFrontWheel = Model.Bones["l_front_wheel_geo"];
            LBackWheel = Model.Bones["l_back_wheel_geo"];
            RFrontWheel = Model.Bones["r_front_wheel_geo"];
            RBackWheel = Model.Bones["r_back_wheel_geo"];
            RFrontSteer = Model.Bones["r_steer_geo"];
            LFrontSteer = Model.Bones["l_steer_geo"];
            Turret = Model.Bones["turret_geo"];
            Canon = Model.Bones["canon_geo"];

            LFrontWheelTransform = LFrontWheel.Transform;
            LBackWheelTransform = LBackWheel.Transform; 
            RFrontWheelTransform = RFrontWheel.Transform;
            RBackWheelTransform = RBackWheel.Transform;
            RFrontSteerTransform = RFrontSteer.Transform;
            LFrontSteerTransform = LFrontSteer.Transform;
            TurretTransform = Turret.Transform;
            CanonTransform = Canon.Transform;

            Textures = new Texture2D[Model.Meshes.Count];

            // this texture indexing will work. for now.
            // we are indexing the textures to the meshes
            int count = 0;
            foreach(ModelMesh mesh in Model.Meshes)
            {

                Textures[count] = ((BasicEffect)mesh.Effects[0]).Texture;

                count++;

            }

            // setup the rasterizer
            SolidRasterizerState = new RasterizerState();

            SolidRasterizerState.FillMode = FillMode.Solid;

            // the blend state
            BlendState = new BlendState();
            BlendState.AlphaBlendFunction = BlendFunction.Add;
            
            // default the ID to 0
            TankID = 0;

            // create a few bullets
            Bullets = new List<Projectile>();
            for (int i = 0; i < 50; i++)
            {
                Projectile p = new Projectile(Game, ProjectilePower);
                
                // init settings here

                Bullets.Add(p);
            }

            // init particles
            Particles = new LineParticleEmitter(Game, Body.Position, 0.5f, 5000);

            Particles.MakeParticles(0.5f, Color.Yellow);
            Particles.ParticleVelocity = new Vector3(0f, 3f, 0f);
            Particles.SpawnRate = 0f;
            Particles.ParticleLifespanMilliseconds = 200f;
            Particles.ParticleLifespanVariationMilliseconds = 50f;
            Particles.Activated = true;

            // create the axis for debug
            Axis = new Axis3D(Game, Body.Position, 50f);
            Game.Components.Add(Axis);
                       
        }

        public void Update(GameTime gameTime, Camera camera, Plane surface)
        {
            // delta for time based calcs
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // pre movement function, to store information
            // regarding previous frame
            Body.PreMovementUpdate(gameTime);
                        
            int dir = Body.Speed > 0f ? -1 : 1;

            // controls rotation
            if (Controls.IsKeyDown(Controls.MovementKeys[TankID, (int)Controls.Cursor.Left]))
            {
                Body.Bounds.Yaw += YawStep * Body.Velocity.Length() * dir * dt;
                SteerAngle += YawStep * dt;

            }
            else if (Controls.IsKeyDown(Controls.MovementKeys[TankID, (int)Controls.Cursor.Right]))
            {
                Body.Bounds.Yaw -= YawStep * Body.Velocity.Length() * dir * dt;
                SteerAngle -= YawStep * dt;
            } else
            {
                SteerAngle *= 0.8f;
            }
            SteerAngle = MathHelper.Clamp(SteerAngle, -MaxSteerAngle, MaxSteerAngle);
          
            // update the model position, based on the updated vectors
            if (Controls.IsKeyDown(Controls.MovementKeys[TankID, (int)Controls.Cursor.Up]))
            {
                Body.Speed -= (Body.Acceleration.Z);
                Particles.Activated = true;

            }
            else if (Controls.IsKeyDown(Controls.MovementKeys[TankID, (int)Controls.Cursor.Down]))
            {
                Body.Speed += (Body.Acceleration.Z);
                Particles.Activated = true;
                
            } else
            {
                Body.Speed = 0f;
                Particles.Activated = false;
            }

            // update the orientation vectors of the tank
            //UpdateDirectionVectors(surface);

            // moves the body
            Body.UpdateMotion(gameTime);

            //UpdateDirectionVectors(surface);
            UpdateMatrices(surface);

            // calculate the particle system position
            // we calculate an offset and a rotation in model space
            // then we transform to world space
            Vector3 offset = new Vector3(1.67f, 2.8f, -3f);
            float pitch = -35f;

            // now we build the particles system own transform
            Matrix particlesTransform = Matrix.CreateRotationX(MathHelper.ToRadians(pitch)) * Matrix.CreateTranslation(offset) * WorldTransform;

            // finally, set the transform and update
            Particles.UpdateMatrices(particlesTransform);
            Particles.Update(gameTime);

            //Console.WriteLine(Canon.ModelTransform);

        }

        public void PostMotionUpdate(GameTime gameTime, Camera camera, Plane surface)
        {

            // keep the tank in the surface
            ConstrainToPlane(surface);

            // adjust height from the terrain surface
            SetHeightFromSurface(surface);

            // update the orientation vectors of the tank
            //UpdateDirectionVectors(surface);

            // update the bones matrices
            UpdateMatrices(surface);

            // update the debug axis
            //Axis.worldMatrix = Matrix.CreateScale(new Vector3(50f) / Scale) * Model.Root.Transform;
            Axis.worldMatrix = WorldTransform;
            Axis.UpdateShaderMatrices(camera.ViewTransform, camera.ProjectionTransform);
        }

        public void UpdateProjectiles(GameTime gameTime, Plane surface)
        {
            // user input
            if (TankID == 0 && Controls.CurrMouseState.LeftButton == ButtonState.Pressed)
            {
                
                // check shooting rate
                if (LastShot < gameTime.TotalGameTime.TotalMilliseconds)
                {

                    // update the shooting rate to now
                    LastShot = (float)gameTime.TotalGameTime.TotalMilliseconds + this.ShootRate;

                    // get the first dead bullet from the pool
                    Projectile b = null;
                    for (int i = 0; i < Bullets.Count; i++)
                    {
                        if (!Bullets[i].Alive)
                        {
                            b = Bullets[i];
                            break;
                        }

                    }

                    // if b not null, we have a bullet
                    if (b != null)
                    {

                        b.Revive();

                        // calculate offset from tank origin
                        Vector3 offset = new Vector3(
                            (float)Math.Cos(MathHelper.ToRadians(TurretYaw) - Body.Bounds.Yaw) * 1.5f,
                            (float)Math.Sin(MathHelper.ToRadians(CanonPitch)) * 1f + 3.5f,
                            -(float)Math.Sin(MathHelper.ToRadians(TurretYaw) - Body.Bounds.Yaw) * 1.5f
                        );

                        Vector3 turretCenterOffset = new Vector3(0f, 0f, -0.35f);

                        b.Body.SetPosition(Body.Position + Vector3.Transform(offset + turretCenterOffset, WorldTransform.Rotation));
                        
                        
                        //b.SetVelocity(CanonPitch, MathHelper.ToRadians(TurretYaw) - Body.Bounds.Yaw);

                        //b.Body.SetPosition(Vector3.Transform(Vector3.Zero, Matrix.CreateFromQuaternion(BoneTransforms[9].Rotation) * Matrix.CreateTranslation(BoneTransforms[10].Translation)));
                        //b.Body.SetPosition(Vector3.Transform(Vector3.Zero, Matrix.CreateRotationY(MathHelper.ToRadians(TurretYaw + 90f) - Body.Bounds.Yaw) * Matrix.CreateTranslation(BoneTransforms[10].Translation)));
                        //b.Body.SetPosition(Vector3.Transform(new Vector3(0, 0, 1), WorldTransform));
                        //b.Body.SetPosition(Vector3.Transform(new Vector3(0, 0, 0), Matrix.CreateRotationY(MathHelper.ToRadians(TurretYaw + 90f) - Body.Bounds.Yaw) * Matrix.CreateRotationX(MathHelper.ToRadians(-CanonPitch)) * WorldTransform));
                        //b.Body.SetPosition(Vector3.Transform(CanonTransform.Translation, WorldTransform));
                        //b.Body.SetPosition(Vector3.Transform(new Vector3(0.0f, 3.2f, 1), Matrix.CreateRotationY(MathHelper.ToRadians(TurretYaw + 90f) - Body.Bounds.Yaw) * WorldTransform));
                        //b.Body.SetPosition(Vector3.Transform(new Vector3(0, 10, 0), WorldTransform));
                        Console.WriteLine($"when need  {BoneTransforms[9].Translation}");

                        b.Body.Velocity = new Vector3(
                            ProjectilePower * (float)Math.Cos(MathHelper.ToRadians(TurretYaw) - Body.Bounds.Yaw),
                            ProjectilePower * (float)Math.Sin(MathHelper.ToRadians(CanonPitch)),
                            ProjectilePower * -(float)Math.Sin(MathHelper.ToRadians(TurretYaw) - Body.Bounds.Yaw)
                        );

                        b.Body.Velocity = Vector3.Transform(b.Body.Velocity, WorldTransform.Rotation);


                    }
                }

            }

            foreach (Projectile b in Bullets)
            {
                b.Update(gameTime, surface);
            }

        }

        public void CalculateAnimations(GameTime gameTime, Camera camera, Plane surface)
        {

            // animate wheels
            RotateWheels(gameTime);

            // update turret and canon direction based on camera direction
            if(TankID == 0)
            {
                if(Global.AimMode == Global.PlayerAimMode.Camera)
                {

                    CanonPitch = ((ThirdPersonCamera)camera).Pitch;
                    // constrain pitch
                    CanonPitch = MathHelper.Clamp(CanonPitch + 30f, -30f, 60f);

                    TurretYaw = ((ThirdPersonCamera)camera).Yaw;

                } else
                {
                    
                }

            }

            RotateTurret(gameTime, CanonPitch, TurretYaw);

            // recalculate
            PostMotionUpdate(gameTime, camera, surface);

        }

        public void Draw(GameTime gameTime, Camera camera, Vector3 lightDirection, Vector4 lightColor, float lightIntensity)
        {

            Game.GraphicsDevice.RasterizerState = this.SolidRasterizerState;
            Game.GraphicsDevice.BlendState = this.BlendState;

            int count = 0;
            foreach (ModelMesh mesh in Model.Meshes)
            {
                
                foreach (ModelMeshPart part in mesh.MeshParts)
                {

                    /*
                     * here we send the data to the shader for processing
                     * see the Diffuse.fx for the implementation
                     */ 

                    part.Effect = Shader;
               
                    // set the shader properties

                    Matrix world = BoneTransforms[mesh.ParentBone.Index];
                    Matrix worldInverseTranspose = Matrix.Transpose(Matrix.Invert(mesh.ParentBone.Transform * world));
                    
                    Shader.Parameters["DirectionLightDirection"].SetValue(lightDirection);

                    Shader.Parameters["World"].SetValue(world);
                    Shader.Parameters["View"].SetValue(camera.ViewTransform);
                    Shader.Parameters["Projection"].SetValue(camera.ProjectionTransform);

                    Shader.Parameters["WorldInverseTranspose"].SetValue(worldInverseTranspose);
                    Shader.Parameters["ViewPosition"].SetValue(camera.Position);

                    Shader.Parameters["MaterialDiffuseTexture"].SetValue(Textures[count]);                    
                    Shader.Parameters["Material2DiffuseTexture"].SetValue(BurrsMap);
                    Shader.Parameters["SpecularMapTexture"].SetValue(SpecularMap);                    
                    Shader.Parameters["NormalMapTexture"].SetValue(NormalMap);

                }

                mesh.Draw();
                count++;

            }

            // render projectiles
            foreach (Projectile b in Bullets)
                b.Draw(gameTime, camera, lightDirection, lightColor, lightIntensity);

            Particles.Draw(gameTime, camera);

            if(Global.ShowHelp)
                BodyDebug.Draw(gameTime, camera);

        }

        public void UpdateMatrices(Plane surface)
        {
            // Up Vector must be already set
            Body.Bounds.UpdateMatrices(GetUpVectorFromTerrain(surface));
            Body.UpdateCollisionRect();
            
            Model.Root.Transform = WorldTransform;

            Console.WriteLine($"before copy {BoneTransforms[9].Translation}");
            Model.CopyAbsoluteBoneTransformsTo(BoneTransforms);
            Console.WriteLine($"after copy {BoneTransforms[9].Translation}");

            BodyDebug.WorldTransform = Body.CollisionRect.WorldTransform;

        }

        public void SetHeightFromSurface(Plane surface)
        {

            // get the nearest vertice from the plane
            // will need to offset 
            int x = (int)Math.Floor((Body.X + surface.Width / 2) / surface.SubWidth);
            int z = (int)Math.Floor((Body.Z + surface.Depth / 2) / surface.SubHeight);

            /* 
             * get the neighbour vertices
             * 
             * 0---1
             * | / |
             * 2---3
             */
            int verticeIndex0 = (surface.XSubs + 1) * z + x;
            int verticeIndex1 = verticeIndex0 + 1;
            int verticeIndex2 = verticeIndex0 + surface.XSubs + 1;
            int verticeIndex3 = verticeIndex2 + 1;

            VertexPositionNormalTexture v0 = surface.VertexList[verticeIndex0];
            VertexPositionNormalTexture v1 = surface.VertexList[verticeIndex1];
            VertexPositionNormalTexture v2 = surface.VertexList[verticeIndex2];
            VertexPositionNormalTexture v3 = surface.VertexList[verticeIndex3];

            // use interpolation to calculate the height at this point in space
            Body.Y = Utils.HeightBilinearInterpolation(Body.Position, v0.Position, v1.Position, v2.Position, v3.Position);

        }
                
        public Vector3 GetUpVectorFromTerrain(Plane surface)
        {

            // get the nearest vertice from the plane
            // will need to offset 
            int x = (int)Math.Floor((Body.Position.X + surface.Width / 2) / surface.SubWidth);
            int z = (int)Math.Floor((Body.Position.Z + surface.Depth / 2) / surface.SubHeight);

            /* 
             * get the neighbour vertices
             * 
             * 0---1
             * | / |
             * 2---3
             */
            int verticeIndex0 = (surface.XSubs + 1) * z + x;
            int verticeIndex1 = verticeIndex0 + 1;
            int verticeIndex2 = verticeIndex0 + surface.XSubs + 1;
            int verticeIndex3 = verticeIndex2 + 1;

            VertexPositionNormalTexture v0 = surface.VertexList[verticeIndex0];
            VertexPositionNormalTexture v1 = surface.VertexList[verticeIndex1];
            VertexPositionNormalTexture v2 = surface.VertexList[verticeIndex2];
            VertexPositionNormalTexture v3 = surface.VertexList[verticeIndex3];

            // interpolate the terrain normals, so we know the tank up vector
            //Up = Utils.NormalBilinearInterpolation(Position, n0.Position, n1.Position, n2.Position, n3.Position);
            //Up = v0.Normal;
            float ratioX0 = 1f - (v1.Position.X - Body.Position.X) / (v1.Position.X - v0.Position.X);
            float ratioX1 = 1f - (v3.Position.X - Body.Position.X) / (v3.Position.X - v2.Position.X);
            float ratioZ = 1f - (v3.Position.Z - Body.Position.Z) / (v2.Position.Z - v0.Position.Z);

            return Utils.NormalBilinearInterpolation(v0.Normal, v1.Normal, v2.Normal, v3.Normal, ratioX0, ratioX1, ratioZ);

        }

        public void ConstrainToPlane(Plane surface)
        {
            // constrain to bounds
            // inset one subdivision

            float halfSurfaceWidth = surface.Width / 2;
            float halfSurfaceDepth = surface.Depth / 2;

            // because we know that the plane origin is at its center
            // we will have to calculate the bounds with that in mind, and add 
            // te width and depth divided by 2
            if (Body.X < -halfSurfaceWidth + surface.SubWidth)
            {

                Body.X = -halfSurfaceWidth + surface.SubWidth;

            }
            if (Body.X > halfSurfaceWidth - surface.SubWidth)
            {

                Body.X = halfSurfaceWidth - surface.SubWidth;

            }
            if (Body.Z < -halfSurfaceDepth + surface.SubHeight)
            {

                Body.Z = -halfSurfaceDepth + surface.SubHeight;

            }
            if (Body.Z > halfSurfaceDepth - surface.SubHeight)
            {

                Body.Z = halfSurfaceDepth - surface.SubHeight;

            }
        }

        private void RotateWheels(GameTime gameTime)
        {
            // rotation based on velocity
            // update wheels angle
            // this line calculates the sign of the moving direction based on the delta position
            // this is the true direction that the tank is moving.
            Vector3 delta = Body.Position - Body.PreviousPosition;
            float dot = Vector3.Dot(delta, Body.Bounds.Front);
            float sign = (dot > 0f ? -1 : 1);
            //Console.WriteLine(dot);
            WheelsAngle += delta.Length() * sign;
            // last bit is to get the sign of the speed

            // the resulting matrix
            Matrix rotationMatrix = Matrix.CreateRotationX(this.WheelsAngle);

            // apply
            LFrontWheel.Transform = rotationMatrix * LFrontWheelTransform;
            LBackWheel.Transform = rotationMatrix * LBackWheelTransform;
            RFrontWheel.Transform = rotationMatrix * RFrontWheelTransform;
            RBackWheel.Transform = rotationMatrix * RBackWheelTransform;


            // rotate the steers           
            
            Matrix rotationRightSteer = Matrix.CreateRotationY(SteerAngle );
            Matrix rotationLeftSteer = Matrix.CreateRotationY(SteerAngle);
            

            RFrontSteer.Transform = rotationRightSteer * RFrontSteerTransform;
            LFrontSteer.Transform = rotationLeftSteer * LFrontSteerTransform;


        }

        // handles rotation of the turret and canon
        private void RotateTurret(GameTime gameTime, float pitch, float yaw)
        {

            

            Matrix turretRotationMatrix = Matrix.CreateRotationY(MathHelper.ToRadians(yaw + 90f) - Body.Bounds.Yaw);
            Matrix canonRotationMatrix = Matrix.CreateRotationX(MathHelper.ToRadians(-CanonPitch));

            // apply
            Turret.Transform = turretRotationMatrix * Matrix.CreateTranslation(TurretTransform.Translation);
            Canon.Transform = canonRotationMatrix * Matrix.CreateTranslation(CanonTransform.Translation);
            
        }

        public string GetDebugInfo()
        {

            return $"TankID: {TankID}\n" +
                   $"Wheels: {WheelsAngle}, TurretYaw: {TurretYaw}, CanonPitch: {CanonPitch}";

        }


    }
}
