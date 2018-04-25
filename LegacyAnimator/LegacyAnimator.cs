﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegacyAnimator : MonoBehaviour {

	[Tooltip("Sample the first frame of this trigger on Start()")]
	public string waitTrigger;

	[Tooltip("Immediately trigger this on Start()")]
	public string autoplayTrigger;

	[Space]
	public LegacyAnimatorNode[] nodes;
	public Dictionary<string,LegacyAnimatorNode> nodeSearch;

	private Dictionary<string,bool> variableBool;
	private Animation animationComponent;

	//When chaining, this will accumulates and can be used to automatically disable the component
	private float cumulativePlayTime;

	private const string waitClipName = "WAIT_CLIP";

    /// <summary>
    /// Kills all animations and play a new one.
    /// </summary>
    public LegacyAnimator SetTrigger(string triggerName)
    {
		Prepare();
		CheckTrigger(triggerName);

		if(animationComponent.enabled == false)
		{
			animationComponent.enabled = true;
			cumulativePlayTime = 0;
		}

		//If it is layered, the play time needs to accommodate the longest clip.
		float playTime = SpeedAdjust(triggerName);

		//If already higher, there might be a longer clip running in other layer.
		if(cumulativePlayTime < playTime)
		{
			cumulativePlayTime = playTime;
		}

        animationComponent.Play(triggerName, PlayMode.StopSameLayer);

		WrapMode wrapMode = animationComponent[triggerName].wrapMode;
		if(wrapMode != WrapMode.Loop && wrapMode != WrapMode.PingPong)
		{
			AutoDisable();
		}
		else
		{
			StopPreviousAutoDisable();
		}

		return this;
	}

    /// <summary>
    /// Chain this with other methods but not the first one.
    /// </summary>
    public LegacyAnimator FollowedBy(string triggerName)
	{
		CheckTrigger(triggerName);
		cumulativePlayTime += SpeedAdjust(triggerName);

		animationComponent.PlayQueued(triggerName,QueueMode.CompleteOthers);

		WrapMode wrapMode = animationComponent[triggerName].wrapMode;
		if(wrapMode != WrapMode.Loop && wrapMode != WrapMode.PingPong)
		{
			AutoDisable();
		}
		else
		{
			StopPreviousAutoDisable();
		}

		return this;
	}

	private float SpeedAdjust(string triggerName)
	{
		float speed = 1 + nodeSearch[triggerName].SpeedAdjust;
		//Debug.Log($"Speed is {speed}");
		if(speed < 0)
		{
			animationComponent[triggerName].time = animationComponent[triggerName].length;
		}
        animationComponent[triggerName].speed = speed;
		return animationComponent[triggerName].length / Mathf.Abs(speed);
	}

    /// <summary>
    /// Kills all animations and wait.
    /// </summary>
	public LegacyAnimator Wait(float seconds)
	{
		cumulativePlayTime = seconds;
		SetWaitTime(seconds);
		animationComponent.enabled = true;

		animationComponent.Stop();
		animationComponent.Play(waitClipName);

		AutoDisable();
		return this;
	}

    /// <summary>
    /// Chain this with other methods but not the first one.
    /// </summary>
    public LegacyAnimator AndWait(float seconds)
	{
		cumulativePlayTime += seconds;
		SetWaitTime(seconds);
		animationComponent.PlayQueued(waitClipName,QueueMode.CompleteOthers);
		AutoDisable();
		return this;
	}

	private void CheckTrigger(string triggerName)
	{
		if(animationComponent[triggerName] == null)
		{
			throw new KeyNotFoundException("No trigger named " + triggerName + " in LegacyAnimator of " + gameObject.name);
		}
	}

	public void Stop()
	{
		animationComponent.Stop();
		AutoDisable();
	}

	public bool IsPlaying(string triggerName)
	{
		CheckTrigger(triggerName);
		return animationComponent[triggerName].enabled;
	}

    /// <summary>
    /// Useful for preparing/hiding something before play. Usually the first frame is the appropriate visual.
	/// Also you can't use FollowedBy after an animation with only the first keyframe. Use this to sample one-frame animation instead of SetTrigger.
    /// </summary>
    public void SampleFirstFrame(string triggerName)
    {
		Prepare();
		CheckTrigger(triggerName);
		animationComponent.enabled = true;
		animationComponent.Stop();
		animationComponent[triggerName].enabled = true;
		animationComponent[triggerName].weight = 1;
		animationComponent.Sample();
		animationComponent[triggerName].enabled = false;
		animationComponent.enabled = false;
	}

    /// <summary>
    /// Triggers the first node
    /// </summary>
    public void Trigger()
    {
        if (nodes.Length < 1)
        {
            throw new System.Exception("Add something to the node..");
        }
        SetTrigger(nodes[0].Trigger);
	}

    /// <summary>
    /// Triggers the second node
    /// </summary>
    public void TriggerSecondary()
	{
        if (nodes.Length < 2)
        {
            throw new System.Exception("Add two things or more to the node..");
        }
        SetTrigger(nodes[1].Trigger);
	}

    private void AutoDisable()
    {
		StopPreviousAutoDisable();
        autoDisableRoutine = AutoDisableRoutine(cumulativePlayTime);
        StartCoroutine(autoDisableRoutine);
    }

    private void StopPreviousAutoDisable()
    {
        if (autoDisableRoutine != null)
        {
            StopCoroutine(autoDisableRoutine);
        }
    }

    private IEnumerator autoDisableRoutine;
    private IEnumerator AutoDisableRoutine(float inTime)
    {
        yield return new WaitForSeconds(inTime);
        yield return null;
        animationComponent.enabled = false;
    }

    private bool prepared = false;
    private void Prepare()
    {
        if (!prepared)
        {
            nodeSearch = new Dictionary<string, LegacyAnimatorNode>();
            variableBool = new Dictionary<string, bool>();
            InitializeAnimationComponent();
            if (nodes != null)
            {
                foreach (LegacyAnimatorNode lan in nodes)
                {
                    animationComponent.AddClip(lan.AnimationClip, lan.Trigger);
                    animationComponent[lan.Trigger].layer = lan.SecondLayer ? 1 : 0;
                    nodeSearch.Add(lan.Trigger, lan);
                }

                AnimationClip waitOneSecond = new AnimationClip();
                waitOneSecond.legacy = true;
                animationComponent.AddClip(waitOneSecond, waitClipName);
            }
        }
        prepared = true;
    }

    [ContextMenu("Match Animation")]
	public void MatchAnimation()
    {
		InitializeAnimationComponent();
        foreach (AnimationState ast in animationComponent)
        {
            //Debug.Log(ast.name + " " + ast.clip.name);
            animationComponent.RemoveClip(ast.clip.name);
        }
        if (nodes != null)
        {
            foreach (LegacyAnimatorNode lan in nodes)
            {
                animationComponent.AddClip(lan.AnimationClip, lan.ClipName);
            }
        }
    }

    private void InitializeAnimationComponent()
	{
        animationComponent = GetComponent<Animation>();
		if(animationComponent != null)
		{
			DestroyImmediate(animationComponent);
		}
        if (animationComponent == null)
        {
            animationComponent = gameObject.AddComponent<Animation>();
        }
        animationComponent.playAutomatically = false;
	}

	public void Awake()
	{
		Prepare();
	}

	public void Start()
	{
        if (waitTrigger != "")
		{
			SampleFirstFrame(waitTrigger);
		}
		if(autoplayTrigger != "")
		{
			SetTrigger(autoplayTrigger);
		}
	}

	public void SetBool(string name, bool val)
	{
		if(variableBool.ContainsKey(name))
		{
			variableBool[name] = val;
		}
		else
		{
			variableBool.Add(name,val);
		}
	}

	public bool GetBool(string name)
	{
		if(variableBool.ContainsKey(name))
		{
			return variableBool[name];
		}
		else
		{
			return false;
		}
	}

	private void SetWaitTime(float seconds)
	{
		foreach(AnimationState ast in animationComponent)
		{
			if(ast.name == waitClipName)
			{
				ast.speed = 1/seconds;
			}
		}
	}


}
