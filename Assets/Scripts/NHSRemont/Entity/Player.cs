using NHSRemont.Gameplay;
using NHSRemont.UI;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;

namespace NHSRemont.Entity
{
    [RequireComponent(typeof(PhotonView), typeof(CharacterMovement))]
    public class Player : MonoBehaviourPun
    {
        public Transform cameraTarget;
        private CharacterMovement movement;
        public Health health { get; private set; }
        private CharacterInventory inventory;
        
        [SerializeField] private float sensitivity = 4f;

        private void Awake()
        {
            if (photonView.IsMine)
            {
                movement = GetComponent<CharacterMovement>();
                health = GetComponent<Health>();
                inventory = GetComponentInChildren<CharacterInventory>();
                transform.Find("Leg L").GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                transform.Find("Leg R").GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        private void Start()
        {
            if (photonView.IsMine)
            {
                MainCamera.target = cameraTarget;
                health.onDeath += GameManager.instance.RespawnPlayerRandomly;
                health.onHealthChanged += GameHUD.instance.UpdateHealthBar;
                inventory.OnHotbarSlotSelected += GameHUD.instance.UpdateSelectedHotbarSlot;
                inventory.onSlotContentsChanged += GameHUD.instance.UpdateHotbarItemDisplay;
            }
        }
        
        private void Update()
        {
            if (photonView.IsMine)
            {
                TakeInput();
                
                if (Input.GetMouseButton(1))
                {
                    ConstructionTest();
                }
                else
                {
                    ExplosionTestInput();
                }
            }
        }
        
        private void TakeInput()
        {
            //Rotation
            Vector2 mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            Vector3 facingAngle = new Vector3(movement.facingAngleX, movement.facingAngleY, 0f);
            facingAngle.x -= mouseDelta.y * sensitivity;
            facingAngle.y += mouseDelta.x * sensitivity;
            movement.SetOrientation(facingAngle);

            //Jumping
            if(Input.GetKeyDown(KeyCode.Space))
                movement.JumpPressed();
            if(Input.GetKeyUp(KeyCode.Space))
                movement.JumpReleased();
            
            //Respawn
            if (Input.GetKeyDown(KeyCode.R))
            {
                GameManager.instance.RespawnPlayerRandomly();
            }
            
            //Inventory
            if (Input.mouseScrollDelta.y != 0)
            {
                inventory.ScrollThroughSlots(-(int)Mathf.Sign(Input.mouseScrollDelta.y));
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                inventory.DropItemsInSlot(inventory.hotbarSlot, 1);
            }
        }

        private void ConstructionTest()
        {
            if (Physics.Raycast(cameraTarget.position, cameraTarget.forward, out RaycastHit hit, 3f))
            {
                Vector3 maxExtents = new Vector3(1.25f, 1.25f, .2f)/2f;
                
                Vector3 fwd = Vector3.Cross(transform.right, hit.normal).normalized;
                Vector3 right = Vector3.Cross(fwd, hit.normal).normalized;
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    (fwd, right) = (right, fwd);
                }
                
                float height = Physics.Raycast(hit.point, hit.normal, out RaycastHit hitU, maxExtents.y*2f) ? hitU.distance : maxExtents.y*2f;
                Debug.DrawRay(hit.point, hit.normal*height, Color.green);

                Vector3 rayPoint = hit.point + hit.normal*height/2f;
                RaycastHit hitR, hitL, hitF, hitB;
                float distR, distL, distF, distB;
                distR = Physics.Raycast(rayPoint, right, out hitR, maxExtents.x) ? hitR.distance : maxExtents.x;
                distL = Physics.Raycast(rayPoint, -right, out hitL, maxExtents.x) ? hitL.distance : maxExtents.x;
                distF = Physics.Raycast(rayPoint, fwd, out hitF, maxExtents.z) ? hitF.distance : maxExtents.z;
                distB = Physics.Raycast(rayPoint, -fwd, out hitB, maxExtents.z) ? hitB.distance : maxExtents.z;
                
                Debug.DrawRay(rayPoint, right*distR, Color.red);
                Debug.DrawRay(rayPoint, -right*distL, Color.yellow);
                Debug.DrawRay(rayPoint, fwd*distF, Color.blue);
                Debug.DrawRay(rayPoint, -fwd*distB, Color.black);

                Vector3 scale = new Vector3(distL + distR, height, distB + distF);
                Vector3 centre = hit.point + right * (distR - distL)/2f + fwd * (distF - distB)/2f +
                                 hit.normal * height / 2f;
                Quaternion rotation = Quaternion.LookRotation(fwd, hit.normal);

                Color32 lineColour = Color.white;
                var overlaps = Physics.OverlapBox(centre, scale/2f * 0.75f, rotation);
                if (overlaps.Length > 0)
                {
                    lineColour = Color.red;
                    foreach (Collider overlap in overlaps)
                    {
                        Debug.DrawLine(centre, overlap.ClosestPoint(centre), Color.red);
                    }
                }
                else if (Input.GetMouseButtonDown(0))
                {
                    Transform cube = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                    cube.localScale = scale;
                    cube.position = centre;
                    cube.rotation = rotation;
                }
                
                Vector3 corner1 = hit.point + right * distR + fwd * distF;
                Vector3 corner2 = hit.point + right * distR - fwd * distB;
                Vector3 corner3 = hit.point - right * distL + fwd * distF;
                Vector3 corner4 = hit.point - right * distL - fwd * distB;
                        
                Debug.DrawLine(corner1, corner2, lineColour);
                Debug.DrawLine(corner2, corner4, lineColour);
                Debug.DrawLine(corner4, corner3, lineColour);
                Debug.DrawLine(corner3, corner1, lineColour);
                
                corner1 += hit.normal*height;
                corner2 += hit.normal*height;
                corner3 += hit.normal*height;
                corner4 += hit.normal*height;
                        
                Debug.DrawLine(corner1, corner2, lineColour);
                Debug.DrawLine(corner2, corner4, lineColour);
                Debug.DrawLine(corner4, corner3, lineColour);
                Debug.DrawLine(corner3, corner1, lineColour);
                        
                Debug.DrawRay(corner1, -hit.normal*height, lineColour);
                Debug.DrawRay(corner2, -hit.normal*height, lineColour);
                Debug.DrawRay(corner3, -hit.normal*height, lineColour);
                Debug.DrawRay(corner4, -hit.normal*height, lineColour);
            }
        }
        
        private void ExplosionTestInput()
        {
            bool clicked = false;

            if (Input.GetMouseButtonDown(0))
                clicked = true;
            else if (Input.GetMouseButton(3)) //rapid-fire
                clicked = true;
            
            if (clicked)
            {
                Transform camTransform = cameraTarget;
                if (Physics.Raycast(camTransform.position, camTransform.forward, out RaycastHit hit, 4000f))
                {
                    float yield = 0.052f;
                    if (Input.GetKey(KeyCode.LeftShift)) yield = 1f;
                    else if (Input.GetKey(KeyCode.Tab)) yield = 6f;
                    else if (Input.GetKey(KeyCode.Z)) yield = 500f;
                    else if (Input.GetKey(KeyCode.Delete)) yield = 27_000f;

                    Vector3 point = hit.point;
                    point -= camTransform.forward * 0.05f;
                    
                    PhysicsManager.instance.CreateExplosion(new ExplosionInfo(point, yield, 0.2f));
                }
            }
        }

        public void Respawn(Transform spawnPoint)
        {
            inventory.DropAllItems();
            transform.position = spawnPoint.position;
            CharacterMovement move = GetComponent<CharacterMovement>();
            move.SetOrientation(spawnPoint.eulerAngles);
            move.StopVelocity();
            health.Revive();
        }
    }
}
