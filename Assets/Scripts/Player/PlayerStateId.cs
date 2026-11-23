using System.Collections.Generic;

public enum PlayerStateId
{
    Locomotion,
    Jump,
    DiveRoll,
    Fall,
    Crouch
}

public interface IPlayerState
{
    void Enter();
    void Tick();
    void Exit();
}

public class PlayerStateMachine
{
    private readonly Dictionary<PlayerStateId, IPlayerState> states = new Dictionary<PlayerStateId, IPlayerState>();

    public IPlayerState CurrentState { get; private set; }
    public PlayerStateId CurrentStateId { get; private set; }

    public void RegisterState(PlayerStateId stateId, IPlayerState state)
    {
        if (states.ContainsKey(stateId))
            states[stateId] = state;
        else
            states.Add(stateId, state);
    }

    public void ChangeState(PlayerStateId newStateId)
    {
        if (!states.ContainsKey(newStateId))
            return;

        CurrentState?.Exit();

        CurrentState = states[newStateId];
        CurrentStateId = newStateId;

        CurrentState.Enter();
    }

    public void Tick()
    {
        CurrentState?.Tick();
    }
}