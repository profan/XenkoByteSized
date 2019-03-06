using System;
using Xenko.Core.Mathematics;
using Xenko.Input;
using Xenko.Engine;
using Xenko.Physics;
using Xenko.Core;

namespace XenkoByteSized {
    public class VehicleScript : SyncScript {

        [Display("Wheel Front Left")]
        public RigidbodyComponent wheelFrontLeft;
        private Generic6DoFConstraint wheelFrontLeftConstraint;

        [Display("Wheel Front Right")]
        public RigidbodyComponent wheelFrontRight;
        private Generic6DoFConstraint wheelFrontRightConstraint;

        [Display("Wheel Back Left")]
        public RigidbodyComponent wheelBackLeft;
        private Generic6DoFConstraint wheelBackLeftConstraint;

        [Display("Wheel Back Right")]
        public RigidbodyComponent wheelBackRight;
        private Generic6DoFConstraint wheelBackRightConstraint;
        
        private RigidbodyComponent vehicleBody;

        public override void Start() {
            vehicleBody = Entity.Get<RigidbodyComponent>();
            SetupPhysicsConstraints();
        }

        static private Generic6DoFConstraint CreateWheelConstraint(RigidbodyComponent parent, RigidbodyComponent wheel) {

            Matrix wheelTranslation = Matrix.Translation(wheel.Entity.Transform.LocalMatrix.TranslationVector);
            Quaternion wheelRotation = wheel.Entity.Transform.Rotation;

            Generic6DoFConstraint wheelConstraint = Simulation.CreateConstraint(
                ConstraintTypes.Generic6DoF, parent, wheel,
                wheelTranslation, /* the constraint itself should be created at the offset where the wheel itself is */
                Matrix.RotationQuaternion(wheelRotation),
                useReferenceFrameA : true
            ) as Generic6DoFConstraint;

            /* lock X and Y rotation */
            wheelConstraint.AngularLowerLimit = new Vector3(0.0f, 0.0f, -(float)Math.PI);
            wheelConstraint.AngularUpperLimit = new Vector3(0.0f, 0.0f, (float)Math.PI);

            return wheelConstraint;

        }

        private void SetupPhysicsConstraints() {

            var sim = this.GetSimulation();

            Generic6DoFConstraint wheel1Constraint = CreateWheelConstraint(vehicleBody, wheelFrontLeft);
            Generic6DoFConstraint wheel2Constraint = CreateWheelConstraint(vehicleBody, wheelFrontRight);
            Generic6DoFConstraint wheel3Constraint = CreateWheelConstraint(vehicleBody, wheelBackLeft);
            Generic6DoFConstraint wheel4Constraint = CreateWheelConstraint(vehicleBody, wheelBackRight);

            sim.AddConstraint(wheel1Constraint);
            wheelFrontLeftConstraint = wheel1Constraint;

            sim.AddConstraint(wheel2Constraint);
            wheelFrontRightConstraint = wheel2Constraint;

            sim.AddConstraint(wheel3Constraint);
            wheelBackLeftConstraint = wheel3Constraint;

            sim.AddConstraint(wheel4Constraint);
            wheelBackRightConstraint = wheel4Constraint;

        }

        public override void Update() {
            
            float dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
            const float FORCE_PER_SECOND = 10;

            if (Input.IsKeyDown(Keys.Right)) {
                vehicleBody.ApplyImpulse(Entity.Transform.WorldMatrix.Left * FORCE_PER_SECOND * dt);
            } else if (Input.IsKeyDown(Keys.Left)) {
                vehicleBody.ApplyImpulse(Entity.Transform.WorldMatrix.Right * FORCE_PER_SECOND * dt);
            }

            DebugText.Print("Left/Right Arrow Key to move the vehicle", new Int2(32, 32));

        }

        public override void Cancel() {
            // FIXME: is this necessary?... either way it crashes at the moment
            wheelFrontLeftConstraint.Dispose();
            wheelFrontRightConstraint.Dispose();
            wheelBackLeftConstraint.Dispose();
            wheelBackRightConstraint.Dispose();
        }

    }
}
