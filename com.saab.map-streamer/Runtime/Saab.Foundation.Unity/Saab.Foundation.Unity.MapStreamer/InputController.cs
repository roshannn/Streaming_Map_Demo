using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
    public enum ActionType
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,
        PanLeft,
        PanRight,
        TiltUp,
        TiltDown,
        SpeedBoost,
        TurnAround,
        GroundPick,
        GroundQuery,
        StopDynamicLoader,
        StartDynamicLoader
    }

    public enum InputTrigger
    {
        Held,
        Pressed
    }

    [Serializable]
    public sealed class InputBinding
    {
        public ActionType Action;
        public KeyCode Key;
        public InputTrigger Trigger;
        public KeyCode Modifier = KeyCode.None;

        public InputBinding(
            ActionType action,
            KeyCode key,
            InputTrigger trigger = InputTrigger.Held,
            KeyCode modifier = KeyCode.None)
        {
            Action = action;
            Key = key;
            Trigger = trigger;
            Modifier = modifier;
        }
    }

    public interface IInputReceiver
    {
        void ProcessInput(ActionType action);
    }

    [DefaultExecutionOrder(-1000)]
    public sealed class InputController : MonoBehaviour
    {
        [SerializeField]
        private List<InputBinding> _bindings = new List<InputBinding>();

        public event Action<ActionType> InputPerformed;

        public IList<InputBinding> Bindings => _bindings;

        private void Reset()
        {
            SetDefaultBindings();
        }

        private void Awake()
        {
            if (_bindings.Count == 0)
                SetDefaultBindings();
        }

        private void Update()
        {
            foreach (var binding in _bindings)
            {
                if (binding.Modifier != KeyCode.None && !Input.GetKey(binding.Modifier))
                    continue;

                var active = binding.Trigger == InputTrigger.Pressed
                    ? Input.GetKeyDown(binding.Key)
                    : Input.GetKey(binding.Key);

                if (active)
                    InputPerformed?.Invoke(binding.Action);
            }
        }

        public void SetDefaultBindings()
        {
            _bindings = new List<InputBinding>
            {
                new InputBinding(ActionType.MoveForward, KeyCode.W),
                new InputBinding(ActionType.MoveBackward, KeyCode.S),
                new InputBinding(ActionType.MoveLeft, KeyCode.A),
                new InputBinding(ActionType.MoveRight, KeyCode.D),
                new InputBinding(ActionType.MoveUp, KeyCode.Space),
                new InputBinding(ActionType.MoveDown, KeyCode.C),
                new InputBinding(ActionType.MoveDown, KeyCode.LeftControl),
                new InputBinding(ActionType.PanLeft, KeyCode.LeftArrow),
                new InputBinding(ActionType.PanRight, KeyCode.RightArrow),
                new InputBinding(ActionType.TiltUp, KeyCode.UpArrow),
                new InputBinding(ActionType.TiltDown, KeyCode.DownArrow),
                new InputBinding(ActionType.SpeedBoost, KeyCode.LeftShift),
                new InputBinding(ActionType.TurnAround, KeyCode.P, InputTrigger.Pressed),
                new InputBinding(
                    ActionType.GroundPick,
                    KeyCode.Mouse0,
                    InputTrigger.Pressed,
                    KeyCode.LeftShift),
                new InputBinding(ActionType.GroundQuery, KeyCode.Mouse1, InputTrigger.Pressed),
                new InputBinding(ActionType.StopDynamicLoader, KeyCode.B),
                new InputBinding(ActionType.StartDynamicLoader, KeyCode.V)
            };
        }
    }
}
