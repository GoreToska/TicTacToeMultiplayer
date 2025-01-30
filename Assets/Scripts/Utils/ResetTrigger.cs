using UnityEngine;

public class ResetTrigger : StateMachineBehaviour
{
    [SerializeField] private string triggerToReset;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.ResetTrigger(triggerToReset);
    }
}