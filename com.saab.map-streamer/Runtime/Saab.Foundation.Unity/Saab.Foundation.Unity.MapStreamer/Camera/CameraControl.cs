using System;

using GizmoSDK.GizmoBase;

using Saab.Foundation.Map;
using Saab.Unity.Extensions;

using UnityEngine;

using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;

namespace Saab.Foundation.Unity.MapStreamer
{
    public struct AutoMovement
    {
        public float forward;
        public float right;
        public float up;
        public float pan;
        public float tilt;
    }

    public sealed class CameraControl : MonoBehaviour
    {
        public float Speed = 20f;
        public float ShiftMultiplier = 2f;
        public float RotSpeed = 20f;

        public double X;
        public double Y;
        public double Z;

        private double _lastRenderTime;
        private double _currentRenderTime;
        private bool _inputLocked = false;
        private AutoMovement _autoMovement = default;
        private float _countDownJump = 4;
        private float _jumpTime = 4;

        public Camera Camera => GetComponent<Camera>();
        public float LodFactor => 2f;

        public Vec3D GlobalPosition
        {
            get => new Vec3D(X, Y, Z);
            set
            {
                X = value.x;
                Y = value.y;
                Z = value.z;
            }
        }

        public float JumpInterval
        {
            get => _jumpTime;
            set
            {
                _jumpTime = value;
                _countDownJump = value;
            }
        }

        public Vector3 Up =>
            MapControl.SystemMap
                .GetLocalOrientation(GlobalPosition)
                .GetCol(2)
                .ToVector3();

        public Vector3 North =>
            MapControl.SystemMap
                .GetLocalOrientation(GlobalPosition)
                .GetCol(1)
                .ToVector3();

        public float GetDeltaTime()
        {
            return _lastRenderTime == 0
                ? 0
                : (float)(_currentRenderTime - _lastRenderTime);
        }

        public void SetSeed(int seed)
        {
            Random.InitState(seed);
        }

        public void RandomJump(float distance, float maxDistance = 3000)
        {
            _countDownJump -= UnityEngine.Time.deltaTime;
            if (_countDownJump > 0)
                return;

            _countDownJump = JumpInterval;
            transform.rotation *= Quaternion.Euler(
                0,
                Random.Range(160, 200),
                0);

            X += (Random.value * 0.5) + 0.5f * distance;
            Z += (Random.value * 0.5) + 0.5f * distance;

            if (X > maxDistance)
                X = 0;
            if (Z > maxDistance)
                Z = 0;
        }

        public double UpdateCamera(double renderTime)
        {
            _lastRenderTime = _currentRenderTime;
            _currentRenderTime = renderTime;

            Move(_autoMovement);
            if (_inputLocked)
                return renderTime;

            var speed = Speed;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= ShiftMultiplier;

            if (Input.GetKey(KeyCode.W))
                MoveForward(speed);
            if (Input.GetKey(KeyCode.S))
                MoveForward(-speed);
            if (Input.GetKey(KeyCode.Space))
                MoveUp(speed / 2);
            if (Input.GetKey(KeyCode.C) ||
                Input.GetKey(KeyCode.LeftControl))
                MoveUp(-speed / 2);
            if (Input.GetKey(KeyCode.D))
                MoveRight(speed);
            if (Input.GetKey(KeyCode.A))
                MoveRight(-speed);

            var rotation = transform.rotation;
            if (Input.GetKey(KeyCode.UpArrow))
                rotation *= Tilt(RotSpeed);
            if (Input.GetKey(KeyCode.DownArrow))
                rotation *= Tilt(-RotSpeed);
            if (Input.GetKey(KeyCode.LeftArrow))
                rotation = Pan(-RotSpeed) * rotation;
            if (Input.GetKey(KeyCode.RightArrow))
                rotation = Pan(RotSpeed) * rotation;
            if (Input.GetKeyDown(KeyCode.P))
                rotation = Quaternion.Euler(0, 180, 0) * rotation;

            transform.rotation = rotation;
            return renderTime;
        }

        private void Update()
        {
            UpdateShaderPosition();
        }

        private void Move(AutoMovement movement)
        {
            MoveForward(movement.forward);
            MoveRight(movement.right);
            MoveUp(movement.up);

            var rotation = transform.rotation;
            rotation *= Tilt(movement.tilt);
            rotation = Pan(-movement.pan) * rotation;
            transform.rotation = rotation;
        }

        private void MoveForward(float moveSpeed)
        {
            X += moveSpeed * GetDeltaTime() * transform.forward.x;
            Y += moveSpeed * GetDeltaTime() * transform.forward.y;
            Z -= moveSpeed * GetDeltaTime() * transform.forward.z;
        }

        private void MoveRight(float moveSpeed)
        {
            X += moveSpeed * GetDeltaTime() * transform.right.x;
            Y += moveSpeed * GetDeltaTime() * transform.right.y;
            Z -= moveSpeed * GetDeltaTime() * transform.right.z;
        }

        private void MoveUp(float moveSpeed)
        {
            X += moveSpeed * GetDeltaTime() * transform.up.x;
            Y += moveSpeed * GetDeltaTime() * transform.up.y;
            Z -= moveSpeed * GetDeltaTime() * transform.up.z;
        }

        private Quaternion Tilt(float rotationSpeed)
        {
            return Quaternion.Euler(
                rotationSpeed * GetDeltaTime(),
                0,
                0);
        }

        private Quaternion Pan(float rotationSpeed)
        {
            return Quaternion.Euler(
                0,
                rotationSpeed * GetDeltaTime(),
                0);
        }

        private void UpdateShaderPosition()
        {
            var position = GlobalPosition;
            const float positionTiling = 5000;
            var cameraHeight = (float)Math.Clamp(
                position.y,
                -float.MaxValue,
                float.MaxValue);
            var worldOffset = new Vector3(
                (float)(position.x % positionTiling),
                cameraHeight,
                -(float)(position.z % positionTiling));
            Shader.SetGlobalVector("_WorldOffset", worldOffset);
        }
    }
}
