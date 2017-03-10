using System;
using UnityEngine;

public class AeroplaneController : MonoBehaviour {
    private float m_MaxEnginePower = 40f;        // The maximum output of the engine.
    private float m_Lift = 0.002f;               // The amount of lift generated by the aeroplane moving forwards.
    private float m_ZeroLiftSpeed = 300;         // The speed at which lift is no longer applied.
    private float m_RollEffect = 1f;             // The strength of effect for roll input.
    private float m_PitchEffect = 1f;            // The strength of effect for pitch input.
    private float m_YawEffect = 0.2f;            // The strength of effect for yaw input.
    private float m_BankedTurnEffect = 0.5f;     // The amount of turn from doing a banked turn.
    private float m_AerodynamicEffect = 0.02f;   // How much aerodynamics affect the speed of the aeroplane.
    private float m_AirBrakesEffect = 3f;        // How much the air brakes effect the drag.
    private float m_ThrottleChangeSpeed = 0.3f;  // The speed with which the throttle changes.
    private float m_DragIncreaseFactor = 0.001f; // how much drag should increase with speed.

    public float throttle;                    // The amount of throttle being used.
    public bool airBrakes;                     // Whether or not the air brakes are being applied.
    public float moveSpeed;             // How fast the aeroplane is traveling in it's forward direction.
    public float enginePower;                // How much power the engine is being given.
    public float roll;
    public float pitch;
    public float rollInput;
    public float pitchInput;
    public float yawInput;
    public float throttleInput;

    private float m_OriginalDrag;         // The drag when the scene starts.
    private float m_OriginalAngularDrag;  // The angular drag when the scene starts.
    private float m_AeroFactor;
    private float m_BankedTurnAmount;
    private Rigidbody m_Rigidbody;

    // these max angles are only used on mobile, due to the way pitch and roll input are handled
    public float maxRollAngle = 80;
    public float maxPitchAngle = 80;

    private bool isCrashed;

    private void Start() {
        m_Rigidbody = GetComponent<Rigidbody>();
        // Store original drag settings, these are modified during flight.
        m_OriginalDrag = m_Rigidbody.drag;
        m_OriginalAngularDrag = m_Rigidbody.angularDrag;
    }

    private void FixedUpdate() {
        // Read input for the pitch, yaw, roll and throttle of the aeroplane.
        this.rollInput = Input.GetAxis("Mouse X");
        this.pitchInput = Input.GetAxis("Mouse Y");
        this.yawInput = Input.GetAxis("Horizontal");
        this.throttleInput = Input.GetAxis("Vertical");

        Move();
    }

    public void Move() {
        // transfer input parameters into properties.s

        if (!isCrashed) {
            
            ClampInputs();
            CalculateRollAndPitchAngles();
            CalculateForwardSpeed();
            ControlThrottle();
            CalculateDrag();
            CaluclateAerodynamicEffect();
            CalculateLinearForces();
            CalculateTorque();
        }
    }


    private void ClampInputs() {
        // clamp the inputs to -1 to 1 range
        rollInput = Mathf.Clamp(rollInput, -1, 1);
        pitchInput = Mathf.Clamp(pitchInput, -1, 1);
        yawInput = Mathf.Clamp(yawInput, -1, 1);
        throttleInput = Mathf.Clamp(throttleInput, -1, 1);
    }


    private void CalculateRollAndPitchAngles() {
        // Calculate roll & pitch angles
        // Calculate the flat forward direction (with no y component).
        var flatForward = transform.forward;
        flatForward.y = 0;
        // If the flat forward vector is non-zero (which would only happen if the plane was pointing exactly straight upwards)
        if (flatForward.sqrMagnitude > 0) {
            flatForward.Normalize();
            // calculate current pitch angle
            var localFlatForward = transform.InverseTransformDirection(flatForward);
            pitch = Mathf.Atan2(localFlatForward.y, localFlatForward.z);
            // calculate current roll angle
            var flatRight = Vector3.Cross(Vector3.up, flatForward);
            var localFlatRight = transform.InverseTransformDirection(flatRight);
            roll = Mathf.Atan2(localFlatRight.y, localFlatRight.x);
        }
    }


    private void CalculateForwardSpeed() {
        // Forward speed is the speed in the planes's forward direction (not the same as its velocity, eg if falling in a stall)
        var localVelocity = transform.InverseTransformDirection(m_Rigidbody.velocity);
        moveSpeed = Mathf.Max(0, localVelocity.z);
    }


    private void ControlThrottle() {


        // Adjust throttle based on throttle input (or immobilized state)
        throttle = Mathf.Clamp01(throttle + throttleInput * Time.deltaTime * m_ThrottleChangeSpeed);

        // current engine power is just:
        enginePower = throttle * m_MaxEnginePower;
    }


    private void CalculateDrag() {
        // increase the drag based on speed, since a constant drag doesn't seem "Real" (tm) enough
        float extraDrag = m_Rigidbody.velocity.magnitude * m_DragIncreaseFactor;
        // Air brakes work by directly modifying drag. This part is actually pretty realistic!
        m_Rigidbody.drag = (airBrakes ? (m_OriginalDrag + extraDrag) * m_AirBrakesEffect : m_OriginalDrag + extraDrag);
        // Forward speed affects angular drag - at high forward speed, it's much harder for the plane to spin
        m_Rigidbody.angularDrag = m_OriginalAngularDrag * moveSpeed;
    }


    private void CaluclateAerodynamicEffect() {
        // "Aerodynamic" calculations. This is a very simple approximation of the effect that a plane
        // will naturally try to align itself in the direction that it's facing when moving at speed.
        // Without this, the plane would behave a bit like the asteroids spaceship!
        if (m_Rigidbody.velocity.magnitude > 0) {
            // compare the direction we're pointing with the direction we're moving:
            m_AeroFactor = Vector3.Dot(transform.forward, m_Rigidbody.velocity.normalized);
            // multipled by itself results in a desirable rolloff curve of the effect
            m_AeroFactor *= m_AeroFactor;
            // Finally we calculate a new velocity by bending the current velocity direction towards
            // the the direction the plane is facing, by an amount based on this aeroFactor
            var newVelocity = Vector3.Lerp(m_Rigidbody.velocity, transform.forward * moveSpeed,
                                           m_AeroFactor * moveSpeed * m_AerodynamicEffect * Time.deltaTime);
            m_Rigidbody.velocity = newVelocity;

            // also rotate the plane towards the direction of movement - this should be a very small effect, but means the plane ends up
            // pointing downwards in a stall
            m_Rigidbody.rotation = Quaternion.Slerp(m_Rigidbody.rotation,
                                                  Quaternion.LookRotation(m_Rigidbody.velocity, transform.up),
                                                  m_AerodynamicEffect * Time.deltaTime);
        }
    }


    private void CalculateLinearForces() {
        // Now calculate forces acting on the aeroplane:
        // we accumulate forces into this variable:
        var forces = Vector3.zero;
        // Add the engine power in the forward direction
        forces += enginePower * transform.forward;
        // The direction that the lift force is applied is at right angles to the plane's velocity (usually, this is 'up'!)
        var liftDirection = Vector3.Cross(m_Rigidbody.velocity, transform.right).normalized;
        // The amount of lift drops off as the plane increases speed - in reality this occurs as the pilot retracts the flaps
        // shortly after takeoff, giving the plane less drag, but less lift. Because we don't simulate flaps, this is
        // a simple way of doing it automatically:
        var zeroLiftFactor = Mathf.InverseLerp(m_ZeroLiftSpeed, 0, moveSpeed);
        // Calculate and add the lift power
        var liftPower = moveSpeed * moveSpeed * m_Lift * zeroLiftFactor * m_AeroFactor;
        forces += liftPower * liftDirection;
        // Apply the calculated forces to the the Rigidbody
        m_Rigidbody.AddForce(forces);
    }


    private void CalculateTorque() {
        // We accumulate torque forces into this variable:
        var torque = Vector3.zero;
        // Add torque for the pitch based on the pitch input.
        torque += pitchInput * m_PitchEffect * transform.right;
        // Add torque for the yaw based on the yaw input.
        torque += yawInput * m_YawEffect * transform.up;
        // Add torque for the roll based on the roll input.
        torque += -rollInput * m_RollEffect * transform.forward;
        // Add torque for banked turning.
        torque += m_BankedTurnAmount * m_BankedTurnEffect * transform.up;
        // The total torque is multiplied by the forward speed, so the controls have more effect at high speed,
        // and little effect at low speed, or when not moving in the direction of the nose of the plane
        // (i.e. falling while stalled)
        m_Rigidbody.AddTorque(torque * moveSpeed * m_AeroFactor);
    }

    void OnCollisionEnter() {
        Debug.Log("Crashed");
        isCrashed = true;
    }
}