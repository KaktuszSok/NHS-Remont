using NHSRemont.Gameplay;
using NHSRemont.Utility;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Entity
{
    [RequireComponent(typeof(Rigidbody), typeof(PhotonView))]
	public class CharacterMovement : MonoBehaviourPun
	{
		[Header("References")]
		[SerializeField] private Transform head;
		[SerializeField] private Rigidbody rb;
		[SerializeField] private SFXCollection footstepSFX;
		private LayerMask characterCollisionMask;

		[Header("Parameters")]
		[SerializeField] private float walkSpeed = 5.5f;
		[SerializeField] private float acceleration = 30f;
		[SerializeField] private float jumpVel = 4f;
		[SerializeField] private float maxSlope = 40f;
		private const float jumpCooldownAmount = 0.1f; //how long a player must wait between jump attempts
		private const float footstepDelay = 60f/229f; //how long between footsteps
		private const float defaultFriction = 0.6f; //friction of the default physics material
		private const float airborneFriction = 0.5f; //friction if in the air

		//Runtime
		public float facingAngleX { get; private set; }
		public float facingAngleY { get; private set; }
		[Header("Runtime")]
		[SerializeField] private bool grounded;
		[SerializeField] private float slope;
		private Vector3 slopeNormal;
		private float friction = 1f;
		private float jumpCooldown = 0f;
		private float jumpPressedTimer = 0f; //allows player to press jump a little too early (while falling back to the ground) and still have it count
		private float footstepTimer = 0f;
		
		private void Awake()
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;

			//get all layers characters collide with
			characterCollisionMask = LayerUtils.GetPhysicsCollisionMask(LayerMask.NameToLayer("Character"));
		}

		private void Start()
		{
			rb.isKinematic = !photonView.IsMine;
			PhysicsManager.instance.RegisterRigidbody(rb, PhysicsManager.PhysObjectType.NORMAL);
		}

		private void FixedUpdate()
		{
			if (photonView.IsMine)
			{
				//movement
				ProcessMovement();
				
				//jumping
				if (jumpCooldown <= 0)
				{
					if (grounded && jumpPressedTimer > 0f && slope <= maxSlope)
					{
						Jump();
						jumpPressedTimer = 0f;
					}
				}
				else
				{
					jumpCooldown -= Time.fixedDeltaTime;
				}
				if(jumpPressedTimer > 0f) //this timer allows for small amount of leeway if pressing jump too early (before hitting the ground)
				{
					jumpPressedTimer -= Time.fixedDeltaTime;
				}
				
				//reset variables for next FixedUpdate (and OnCollisionStay)
				grounded = false;
				slope = 0f;
				slopeNormal = Vector3.up;
				friction = airborneFriction;
			}
		}

		private void ProcessMovement()
		{
			//Movement
			Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
			Vector3 velocity = rb.velocity;
			Vector3 localVel = transform.InverseTransformDirection(velocity);

			//calculate movement speed
			float maxSpeedBoostDueToLowFriction = (1 - friction) * (1 - friction) * (1 - friction);
			float speed = walkSpeed * (1 + maxSpeedBoostDueToLowFriction);
			speed *= Mathf.Cos(Mathf.Min(slope, maxSlope) * Mathf.Deg2Rad);
			
			//get movement vector accounting for slope
			Vector3 forwardInclSlope = Vector3.Cross(transform.right, slopeNormal);
			Vector3 rightInclSlope = Vector3.Cross(slopeNormal, transform.forward);
			
			//slope sliding physics
			float factorFwd = 1f, factorRight = 1f;
			if (slope > maxSlope)
			{
				forwardInclSlope = transform.forward;
				rightInclSlope = transform.right;

				Vector3 slopeBlockingNormal = new Vector3(slopeNormal.x, 0f, slopeNormal.z).normalized;

				if (input.x == 0) factorRight = 0f;
				else
				{
					factorRight = Vector3.Dot(rightInclSlope*Mathf.Sign(input.x), slopeBlockingNormal);
					factorRight = 1 + Mathf.Min(factorRight, 0f);
				}
				
				if (input.y == 0) factorFwd = 0f;
				else
				{
					factorFwd = Vector3.Dot(forwardInclSlope*Mathf.Sign(input.y), slopeBlockingNormal);
					factorFwd = 1 + Mathf.Min(factorFwd, 0f);
				}

				if (input.x != 0 || input.y != 0)
				{
					Vector3 combinedInclSlope = forwardInclSlope * input.y + rightInclSlope * input.x;
					float factorCombined = Vector3.Dot(combinedInclSlope, slopeBlockingNormal);
					factorCombined = 1 + Mathf.Min(factorCombined, 0f);
					factorFwd = Mathf.Min(factorFwd, factorCombined);
					factorRight = Mathf.Min(factorRight, factorCombined);
				}
				
				if (Vector3.Dot(forwardInclSlope * Mathf.Sign(input.y), slopeBlockingNormal) < 0f)
				{
					forwardInclSlope = Vector3.ProjectOnPlane(forwardInclSlope, slopeBlockingNormal);
				}
				if(Vector3.Dot(rightInclSlope*Mathf.Sign(input.x), slopeNormal) < 0f)
				{
					rightInclSlope = Vector3.ProjectOnPlane(rightInclSlope, slopeBlockingNormal);
				}
			}
			Debug.DrawRay(transform.position, forwardInclSlope, Color.blue, Time.fixedDeltaTime);
			Debug.DrawRay(transform.position, rightInclSlope, Color.yellow, Time.fixedDeltaTime);

			//make strafing more responsive by cancelling velocity when switching keys
			bool decelerateX = (localVel.x < 0 && input.x > 0 || localVel.x > 0 && input.x < 0);
			bool decelerateZ = (localVel.z < 0 && input.y > 0 || localVel.z > 0 && input.y < 0);

			Vector3 desiredVel_world = ((forwardInclSlope * input.y) + (rightInclSlope * input.x))*speed;
			Vector3 desiredVel_local = transform.InverseTransformDirection(desiredVel_world);

			localVel.x = Mathf.MoveTowards(localVel.x, desiredVel_local.x,
				acceleration * factorRight * (decelerateX ? 2f : 1f) * friction * Time.fixedDeltaTime);
			localVel.z = Mathf.MoveTowards(localVel.z, desiredVel_local.z,
				acceleration * factorFwd * (decelerateZ ? 2f : 1f) * friction * Time.fixedDeltaTime);
			if (grounded && slope <= maxSlope)
			{
				localVel.y = Mathf.MoveTowards(localVel.y, desiredVel_local.y,
					acceleration * Time.fixedDeltaTime);
			}
			
			velocity = transform.TransformDirection(localVel);
			Debug.DrawRay(transform.position, velocity, Color.cyan, Time.fixedDeltaTime);
			rb.velocity = velocity;
			
			//play footstep sound
			if (footstepSFX != null)
			{
				if (footstepTimer > 0f)
				{
					footstepTimer -= Time.fixedDeltaTime;
				}

				if (footstepTimer <= 0f && grounded && slope <= maxSlope && input != Vector2.zero)
				{
					footstepTimer = footstepDelay;
					footstepSFX.PlayRandomSoundAtPosition(transform.position);
				}
			}
		}

		public void JumpPressed()
		{
			jumpPressedTimer = 0.2f;
		}

		public void JumpReleased()
		{
			jumpPressedTimer = 0f;
		}

		private void Jump()
		{
			rb.AddForce(Vector3.up*jumpVel, ForceMode.VelocityChange);
			grounded = false;
			jumpCooldown = jumpCooldownAmount;
		}

		public void SetOrientation(Vector3 euler)
		{
			euler.x = Mathf.Clamp(euler.x, -90f, 90f);
			euler.y = Mathf.Repeat(euler.y, 360f);
			
			facingAngleX = euler.x;
			facingAngleY = euler.y;
			Vector3 bodyEuler = transform.eulerAngles;
			bodyEuler.y = facingAngleY;
			transform.eulerAngles = bodyEuler;
			head.localEulerAngles = Vector3.right * facingAngleX;
		}

		public void StopVelocity()
		{
			rb.velocity = Vector3.zero;
		}

		private void OnCollisionStay(Collision collision)
		{
			if(!photonView.IsMine) return;
			
			ContactPoint[] contacts = new ContactPoint[collision.contactCount];
			collision.GetContacts(contacts);
			for (var i = 0; i < contacts.Length; i++)
			{
				float contactSlope = Mathf.Acos(Mathf.Clamp(contacts[i].normal.y, -1f, 1f)) * Mathf.Rad2Deg;
				if(contactSlope > 90f) continue;

				if (!grounded || contactSlope < slope) //haven't touched any slopes before - mark as grounded and save slope
				{
					grounded = true;
					slope = contactSlope;
					slopeNormal = contacts[i].normal;
					friction = collision.collider.material.dynamicFriction / defaultFriction;
				}
			}
		}
	}
}
