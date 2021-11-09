using System.Collections;
using System.Collections.Generic;
using NHSRemont.Utility;
using UnityEngine;

namespace NHSRemont
{
    [RequireComponent(typeof(Rigidbody))]
	public class PlayerMovement : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private Transform cam;
		[SerializeField] private Rigidbody rb;
		[SerializeField] private Transform groundCheckOrigin;
		[SerializeField] private SFXCollection footstepSFX;

		[Header("Parameters")]
		[SerializeField]
		private float sensitivity = 4f;
		[SerializeField] private float walkSpeed = 5f;
		[SerializeField] private float acceleration = 75f;
		[SerializeField] private float jumpVel = 4f;
		[SerializeField] private float maxSlope = 40f;
		[SerializeField] private float maxStep = 0.25f;
		[SerializeField] private float bodyRadius = 0.45f;
		[SerializeField] private float bodyHeight = 1.8f;
		private LayerMask playerCollisionMask;

		//Runtime
		private Vector3 velocity;
		private float camAngle; //X rotation of camera (up/down)
		[Header("Runtime")]
		[SerializeField]
		private bool grounded;
		[SerializeField] private float slope;
		private const float jumpCooldownAmount = 0.1f; //how long a player must wait between jump attempts
		private float jumpCooldown = 0f;
		private float jumpPressedTimer = 0f; //allows player to press jump a little too early (while falling back to the ground) and still have it count
		private const float footstepDelay = 60f/229f; //how long between footsteps
		private float footstepTimer = 0f;
		
		private void Awake()
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;

			//get all layers the player collides with
			playerCollisionMask = LayerUtils.GetPhysicsCollisionMask(LayerMask.NameToLayer("Player"));
		}

		private void Update()
		{
			//Rotation
			Vector2 mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
			camAngle = Mathf.Clamp(camAngle - mouseDelta.y*sensitivity, -90, 90);
			transform.Rotate(Vector3.up, mouseDelta.x * sensitivity, Space.World);
			cam.localEulerAngles = Vector3.right * camAngle;

			if(jumpPressedTimer > 0f)
			{
				jumpPressedTimer -= Time.deltaTime;
			}
			if (Input.GetKeyDown(KeyCode.Space))
				jumpPressedTimer = 0.2f;
			else if (Input.GetKeyUp(KeyCode.Space))
				jumpPressedTimer = 0f;

			if (jumpCooldown <= 0)
			{
				if (grounded && slope <= maxSlope && jumpPressedTimer > 0f)
				{
					Jump();
				}
			}
			else
			{
				jumpCooldown -= Time.deltaTime;
			}
		}

		private void FixedUpdate()
		{
			//Movement
			Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

			//check if we're on the ground
			grounded = GroundCheck(out RaycastHit groundHit) && jumpCooldown <= 0f;

			//get ground normal
			RaycastHit normalHit = new RaycastHit();
			if (grounded)
			{
				StepCheck(groundHit.point, out normalHit, true);
				//get the slope we're standing on
				if (normalHit.collider != null)
				{
					slope = Mathf.Acos(Mathf.Clamp(normalHit.normal.y, -1f, 1f)) * Mathf.Rad2Deg;
				}
				else slope = 0f;
			}
			else slope = 0f;

			//calculate movement speed
			float speed = walkSpeed;
			speed *= Mathf.Cos(slope * Mathf.Deg2Rad);
			
			//set velocity to movement vector
			Vector3 slopeNormal = (slope > maxSlope || normalHit.collider == null) ? Vector3.up : normalHit.normal;
			Vector3 forwardInclSlope = Vector3.Cross(transform.right, slopeNormal);
			Vector3 rightInclSlope = Vector3.Cross(slopeNormal, transform.forward);
			if (grounded && slope <= maxSlope)
			{
				velocity.y = 0;
			}
			else if(velocity.y > 0 && HeadCheck())
			{
				velocity.y = 0;
			}
			//make strafing more responsive by cancelling velocity when switching keys
			Vector3 localVel = transform.InverseTransformDirection(velocity);
			if (localVel.x < 0 && input.x > 0 || localVel.x > 0 && input.x < 0)
				localVel.x = 0;
			if (localVel.z < 0 && input.y > 0 || localVel.z > 0 && input.y < 0)
				localVel.z = 0;
			velocity = transform.TransformDirection(localVel);
			Vector3 walkVelocity = ((forwardInclSlope * input.y) + (rightInclSlope * input.x))*speed;
			velocity = Vector3.MoveTowards(velocity, walkVelocity + Vector3.up*velocity.y, acceleration*Time.fixedDeltaTime);
			velocity += Physics.gravity * Time.fixedDeltaTime;

			rb.velocity = velocity;
			
			//play footstep sound
			if (footstepSFX != null)
			{
				if (footstepTimer > 0f)
				{
					footstepTimer -= Time.fixedDeltaTime;
				}

				if (footstepTimer <= 0f && grounded && input != Vector2.zero && slope <= maxSlope)
				{
					footstepTimer = footstepDelay;
					footstepSFX.PlayRandomSoundAtPosition(transform.position);
				}
			}
		}

		private void Jump()
		{
			velocity = new Vector3(velocity.x, jumpVel, velocity.z);
			grounded = false;
			jumpCooldown = jumpCooldownAmount;
		}

		private bool GroundCheck(out RaycastHit groundHit, Vector3 offset = default)
		{
			float scale = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
			//try a spherecast to find what we are standing on
			return Physics.SphereCast(groundCheckOrigin.position + offset, bodyRadius*scale, Vector3.down, out groundHit, 
				0.01f + (groundCheckOrigin.localPosition.y*groundCheckOrigin.lossyScale.y) - (bodyRadius*scale), playerCollisionMask, QueryTriggerInteraction.Ignore);
		}

		private bool StepCheck(Vector3 groundContactPoint, out RaycastHit stepHit, bool canStepDown)
		{
			//figure out where we want to step on
			Vector3 stepOrigin = groundContactPoint + Vector3.up*0.01f;
			//do a raycast to where we're stepping
			float rayDist = (groundContactPoint.y - rb.position.y) + 0.02f + (maxStep + (canStepDown ? maxStep : 0f))*transform.localScale.y;

			Debug.DrawRay(stepOrigin, Vector3.down * rayDist);
			if(Physics.Raycast(stepOrigin, Vector3.down, out stepHit, rayDist, playerCollisionMask, QueryTriggerInteraction.Ignore))
			{
				return true;
			}

			return false;
		}

		private bool HeadCheck()
		{
			float scale = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
			//try a spherecast to see if we bumped our head
			return Physics.SphereCast(rb.position + Vector3.up*(bodyHeight*transform.localScale.y - bodyRadius*scale - 0.1f), bodyRadius*scale, Vector3.up,
				out RaycastHit _, 0.11f, playerCollisionMask, QueryTriggerInteraction.Ignore);
		}
	}
}
