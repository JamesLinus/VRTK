﻿// Physics Slider|PhysicsControllables|110040
namespace VRTK.Controllables.PhysicsBased
{
    using UnityEngine;
    using VRTK.GrabAttachMechanics;
    using VRTK.SecondaryControllerGrabActions;

    /// <summary>
    /// A physics based slider.
    /// </summary>
    /// <remarks>
    /// **Required Components:**
    ///  * `Collider` - A Unity Collider to determine when an interaction has occured. Can be a compound collider set in child GameObjects. Will be automatically added at runtime.
    ///  * `Rigidbody` - A Unity Rigidbody to allow the GameObject to be affected by the Unity Physics System. Will be automatically added at runtime.
    ///
    /// **Optional Components:**
    ///  * `VRTK_ControllerRigidbodyActivator` - A Controller Rigidbody Activator to automatically enable the controller rigidbody when near the slider. Will be automatically created if the `Auto Interaction` paramter is checked.
    /// 
    /// **Script Usage:**
    ///  * Create a slider container GameObject and set the GameObject that is to become the slider as a child of the container.
    ///  * Place the `VRTK_PhysicsSlider` script onto the GameObject that is to become the slider.
    ///
    ///   > The slider GameObject must not be at the root level and needs to have it's Transform position set to `0,0,0`. This is the reason for the container GameObject requirement. Any positioning of the slider must be set on the parent GameObject.
    /// </remarks>
    [AddComponentMenu("VRTK/Scripts/Interactables/Controllables/Physics/VRTK_PhysicsSlider")]
    public class VRTK_PhysicsSlider : VRTK_BasePhysicsControllable
    {
        [Header("Slider Settings")]

        [Tooltip("The maximum length that the slider can be moved from the origin position across the `Operate Axis`. A negative value will allow it to move the opposite way.")]
        public float maximumLength = 0.1f;
        [Tooltip("The normalized position the slider can be within the minimum or maximum slider positions before the minimum or maximum positions are considered reached.")]
        public float minMaxThreshold = 0.01f;
        [Tooltip("The target position to move the slider towards given in a normalized value of `0f` (start point) to `1f` (end point).")]
        [Range(0f, 1f)]
        public float positionTarget = 0f;
        [Tooltip("The position the slider when it is at the default resting point given in a normalized value of `0f` (start point) to `1f` (end point).")]
        [Range(0f, 1f)]
        public float restingPosition = 0f;
        [Tooltip("The normalized threshold value the slider has to be within the `Resting Position` before the slider is forced back to the `Resting Position` if it is not grabbed.")]
        [Range(0f, 1f)]
        public float forceRestingPositionThreshold = 0f;

        [Header("Value Step Settings")]

        [Tooltip("The minimum `(x)` and the maximum `(y)` step values for the slider to register along the `Operate Axis`.")]
        public Vector2 stepValueRange = new Vector3(0f, 1f);
        [Tooltip("The increments the slider value will change in between the `Step Value Range`.")]
        public float stepSize = 0.1f;
        [Tooltip("If this is checked then the value for the slider will be the step value and not the absolute position of the slider Transform.")]
        public bool useStepAsValue = true;

        [Header("Snap Settings")]

        [Tooltip("If this is checked then the slider will snap to the position of the nearest step along the value range.")]
        public bool snapToStep = false;
        [Tooltip("The speed in which the slider will snap to the relevant point along the `Operate Axis`")]
        public float snapForce = 10f;

        [Header("Interaction Settings")]

        [Tooltip("The maximum distance the grabbing object is away from the slider before it is automatically released.")]
        public float detachDistance = 1f;
        [Tooltip("The amount of friction to the slider Rigidbody when it is released.")]
        public float releaseFriction = 10f;
        [Tooltip("A collection of GameObjects that will be used as the valid collisions to determine if the door can be interacted with.")]
        public GameObject[] onlyInteractWith = new GameObject[0];

        protected ConfigurableJoint controlJoint;
        protected bool createCustomJoint;
        protected VRTK_InteractableObject controlInteractableObject;
        protected VRTK_TrackObjectGrabAttach controlGrabAttach;
        protected VRTK_SwapControllerGrabAction controlSecondaryGrabAction;
        protected bool createControlInteractableObject;
        protected Vector3 targetLocalPosition;
        protected Vector3 previousLocalPosition;
        protected float previousPositionTarget;
        protected bool stillResting;

        /// <summary>
        /// The GetValue method returns the current position value of the slider.
        /// </summary>
        /// <returns>The actual position of the button.</returns>
        public override float GetValue()
        {
            return transform.localPosition[(int)operateAxis];
        }

        /// <summary>
        /// The GetNormalizedValue method returns the current position value of the slider normalized between `0f` and `1f`.
        /// </summary>
        /// <returns>The normalized position of the button.</returns>
        public override float GetNormalizedValue()
        {
            return VRTK_SharedMethods.NormalizeValue(GetValue(), originalLocalPosition[(int)operateAxis], MaximumLength()[(int)operateAxis]);
        }

        /// <summary>
        /// The GetStepValue method returns the current position of the slider based on the step value range.
        /// </summary>
        /// <param name="currentValue">The current position value of the slider to get the Step Value for.</param>
        /// <returns>The current Step Value based on the slider position.</returns>
        public virtual float GetStepValue(float currentValue)
        {
            return Mathf.Round((stepValueRange.x + Mathf.Clamp01(currentValue / maximumLength) * (stepValueRange.y - stepValueRange.x)) / stepSize) * stepSize;
        }

        /// <summary>
        /// The SetTargetPositionWithStepValue sets the `Position Target` parameter but uses a value within the `Step Value Range`.
        /// </summary>
        /// <param name="givenStepValue">The step value within the `Step Value Range` to set the `Position Target` parameter to.</param>
        public virtual void SetPositionTargetWithStepValue(float givenStepValue)
        {
            positionTarget = VRTK_SharedMethods.NormalizeValue(givenStepValue, stepValueRange.x, stepValueRange.y);
            SetPositionWithNormalizedValue(positionTarget);
        }

        /// <summary>
        /// The SetRestingPositionWithStepValue sets the `Resting Position` parameter but uses a value within the `Step Value Range`.
        /// </summary>
        /// <param name="givenStepValue">The step value within the `Step Value Range` to set the `Resting Position` parameter to.</param>
        public virtual void SetRestingPositionWithStepValue(float givenStepValue)
        {
            restingPosition = VRTK_SharedMethods.NormalizeValue(givenStepValue, stepValueRange.x, stepValueRange.y);
        }

        /// <summary>
        /// The GetPositionFromStepValue returns the position the slider would be at based on the given step value.
        /// </summary>
        /// <param name="givenStepValue">The step value to check the position for.</param>
        /// <returns>The position the slider would be at based on the given step value.</returns>
        public virtual float GetPositionFromStepValue(float givenStepValue)
        {
            float normalizedStepValue = VRTK_SharedMethods.NormalizeValue(givenStepValue, stepValueRange.x, stepValueRange.y);
            return Mathf.Lerp(controlJoint.linearLimit.limit, -controlJoint.linearLimit.limit, Mathf.Clamp01(normalizedStepValue));
        }

        /// <summary>
        /// The IsResting method returns whether the slider is currently in a resting state at the resting position or within the resting position threshold and not grabbed.
        /// </summary>
        /// <returns>Returns `true` if the slider is at the resting position or within the resting position threshold.</returns>
        public override bool IsResting()
        {
            float currentValue = GetNormalizedValue();
            return (!IsGrabbed() && currentValue <= (restingPosition + forceRestingPositionThreshold) && currentValue >= (restingPosition - forceRestingPositionThreshold));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetupInteractableObject();
            SetupJoint();
            targetLocalPosition = Vector3.zero;
            previousLocalPosition = Vector3.one * float.MaxValue;
            previousPositionTarget = float.MaxValue;
            stillResting = false;
            SetPositionWithNormalizedValue(positionTarget);
        }

        protected override void OnDisable()
        {
            if (createControlInteractableObject)
            {
                ManageInteractableObjectListeners(false);
                Destroy(controlSecondaryGrabAction);
                Destroy(controlGrabAttach);
                Destroy(controlInteractableObject);
            }
            else
            {
                ManageInteractableObjectListeners(false);
            }
            if (createCustomJoint)
            {
                Destroy(controlJoint);
            }
            base.OnDisable();
        }

        protected virtual void Update()
        {
            ForceRestingPosition();
            ForcePositionTarget();
            ForceSnapToStep();
            EmitEvents();
        }

        protected override void OnDrawGizmosSelected()
        {
            Vector3 initialPoint = (Application.isPlaying ? originalPosition : transform.position);
            base.OnDrawGizmosSelected();
            Vector3 objectHalf = AxisDirection(true) * (transform.lossyScale[(int)operateAxis] * 0.5f);
            Vector3 destinationPoint = initialPoint + (AxisDirection(true) * maximumLength);
            Gizmos.DrawLine(initialPoint, destinationPoint);
            Gizmos.DrawSphere(initialPoint, 0.01f);
            Gizmos.DrawSphere(destinationPoint, 0.01f);
        }

        protected override void ConfigueRigidbody()
        {
            SetRigidbodyGravity(false);
            controlRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            controlRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        protected override ControllableEventArgs EventPayload()
        {
            ControllableEventArgs e = base.EventPayload();
            e.value = (useStepAsValue ? GetStepValue(GetValue()) : GetValue());
            return e;
        }

        protected virtual void ForceRestingPosition()
        {
            if (forceRestingPositionThreshold > 0f && !IsGrabbed() && (Mathf.Abs(restingPosition - GetNormalizedValue()) <= forceRestingPositionThreshold))
            {
                SetPositionWithNormalizedValue(restingPosition);
                EnableJointDriver();
            }
        }

        protected virtual void ForcePositionTarget()
        {
            if (!IsGrabbed() && positionTarget != previousPositionTarget)
            {
                SetPositionWithNormalizedValue(positionTarget);
                EnableJointDriver();
            }
            previousPositionTarget = positionTarget;
        }

        protected virtual void ForceSnapToStep()
        {
            if (snapToStep && controlJoint != null && !IsGrabbed() && controlJoint.targetPosition == Vector3.zero && Mathf.Abs(GetValue() - GetPositionFromStepValue(GetStepValue(GetValue()))) >= equalityFidelity)
            {
                SetPositionTargetWithStepValue(GetStepValue(GetValue()));
            }
        }

        protected virtual void SetPositionWithNormalizedValue(float givenTargetPosition)
        {
            float positionOnAxis = Mathf.Lerp(controlJoint.linearLimit.limit, -controlJoint.linearLimit.limit, Mathf.Clamp01(givenTargetPosition));
            SnapToPosition(positionOnAxis);
        }

        protected virtual void SnapToPosition(float positionOnAxis)
        {
            if (controlJoint != null)
            {
                controlJoint.targetPosition = (AxisDirection() * Mathf.Sign(maximumLength)) * positionOnAxis;
            }
        }

        protected virtual Vector3 MaximumLength()
        {
            return originalLocalPosition + (AxisDirection() * maximumLength);
        }

        protected virtual void SetupInteractableObject()
        {
            createControlInteractableObject = false;
            controlInteractableObject = GetComponent<VRTK_InteractableObject>();
            if (controlInteractableObject == null)
            {
                controlInteractableObject = gameObject.AddComponent<VRTK_InteractableObject>();
                createControlInteractableObject = true;
                controlInteractableObject.isGrabbable = true;
                controlInteractableObject.ignoredColliders = (onlyInteractWith.Length > 0 ? VRTK_SharedMethods.ColliderExclude(GetComponentsInChildren<Collider>(true), VRTK_SharedMethods.GetCollidersInGameObjects(onlyInteractWith, true, true)) : new Collider[0]);

                SetupGrabMechanic();
                SetupSecondaryAction();
                ManageInteractableObjectListeners(true);
            }
        }

        protected virtual void SetupGrabMechanic()
        {
            controlGrabAttach = controlInteractableObject.gameObject.AddComponent<VRTK_TrackObjectGrabAttach>();
            SetGrabMechanicParameters();
            controlInteractableObject.grabAttachMechanicScript = controlGrabAttach;
        }

        protected virtual void SetGrabMechanicParameters()
        {
            if (controlGrabAttach != null)
            {
                controlGrabAttach.precisionGrab = true;
                controlGrabAttach.detachDistance = detachDistance;
            }
        }

        protected virtual void SetupSecondaryAction()
        {
            controlSecondaryGrabAction = controlInteractableObject.gameObject.AddComponent<VRTK_SwapControllerGrabAction>();
            controlInteractableObject.secondaryGrabActionScript = controlSecondaryGrabAction;
        }

        protected virtual void SetupJoint()
        {
            //move transform towards activation distance
            transform.localPosition += AxisDirection() * (maximumLength * 0.5f);

            controlJoint = GetComponent<ConfigurableJoint>();
            createCustomJoint = false;
            if (controlJoint == null)
            {
                controlJoint = gameObject.AddComponent<ConfigurableJoint>();
                createCustomJoint = true;

                controlJoint.angularXMotion = ConfigurableJointMotion.Locked;
                controlJoint.angularYMotion = ConfigurableJointMotion.Locked;
                controlJoint.angularZMotion = ConfigurableJointMotion.Locked;

                controlJoint.xMotion = (operateAxis == OperatingAxis.xAxis ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked);
                controlJoint.yMotion = (operateAxis == OperatingAxis.yAxis ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked);
                controlJoint.zMotion = (operateAxis == OperatingAxis.zAxis ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked);

                SoftJointLimit linearLimit = new SoftJointLimit();
                linearLimit.limit = Mathf.Abs(maximumLength * 0.5f);
                controlJoint.linearLimit = linearLimit;
                controlJoint.connectedBody = connectedTo;

                EnableJointDriver();
            }
        }

        protected virtual void EnableJointDriver()
        {
            SetJointDrive(snapForce);
        }

        protected virtual void DisableJointDriver()
        {
            SetJointDrive(0f);
        }

        protected virtual void SetJointDrive(float driverForce)
        {
            JointDrive snapDriver = new JointDrive();
            snapDriver.positionSpring = 1000f;
            snapDriver.positionDamper = 100f;
            snapDriver.maximumForce = driverForce;

            controlJoint.xDrive = snapDriver;
            controlJoint.yDrive = snapDriver;
            controlJoint.zDrive = snapDriver;
        }

        protected virtual void ManageInteractableObjectListeners(bool state)
        {
            if (controlInteractableObject != null)
            {
                if (state)
                {
                    controlInteractableObject.InteractableObjectGrabbed += InteractableObjectGrabbed;
                    controlInteractableObject.InteractableObjectUngrabbed += InteractableObjectUngrabbed;
                }
                else
                {
                    controlInteractableObject.InteractableObjectGrabbed -= InteractableObjectGrabbed;
                    controlInteractableObject.InteractableObjectUngrabbed -= InteractableObjectUngrabbed;
                }
            }
        }

        protected virtual void InteractableObjectGrabbed(object sender, InteractableObjectEventArgs e)
        {
            SetGrabMechanicParameters();
            SetRigidbodyDrag(0f);
            DisableJointDriver();
        }

        protected virtual void InteractableObjectUngrabbed(object sender, InteractableObjectEventArgs e)
        {
            SetRigidbodyDrag(releaseFriction);
            if (snapToStep)
            {
                SetPositionTargetWithStepValue(GetStepValue(GetValue()));
                EnableJointDriver();
            }
        }

        protected virtual bool IsGrabbed()
        {
            return (controlInteractableObject != null && controlInteractableObject.IsGrabbed());
        }

        protected virtual void EmitEvents()
        {
            bool valueChanged = !VRTK_SharedMethods.Vector3ShallowCompare(transform.localPosition, previousLocalPosition, equalityFidelity);

            if (valueChanged)
            {
                float currentPosition = GetNormalizedValue();
                float minThreshold = minMaxThreshold;
                float maxThreshold = 1f - minMaxThreshold;
                stillResting = false;

                ControllableEventArgs payload = EventPayload();
                OnValueChanged(payload);

                if (currentPosition >= maxThreshold && !AtMaxLimit())
                {
                    atMaxLimit = true;
                    OnMaxLimitReached(payload);
                }
                else if (currentPosition <= minThreshold && !AtMinLimit())
                {
                    atMinLimit = true;
                    OnMinLimitReached(payload);
                }
                else if (currentPosition > minThreshold && currentPosition < maxThreshold)
                {
                    if (AtMinLimit())
                    {
                        OnMinLimitExited(payload);
                    }
                    if (AtMaxLimit())
                    {
                        OnMaxLimitExited(payload);
                    }

                    atMinLimit = false;
                    atMaxLimit = false;
                }
            }

            if (!stillResting && IsResting() && !valueChanged)
            {
                OnRestingPointReached(EventPayload());
                stillResting = true;
            }

            previousLocalPosition = transform.localPosition;
        }
    }
}