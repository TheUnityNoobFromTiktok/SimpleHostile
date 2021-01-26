using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;


public class Player : MonoBehaviourPunCallbacks
{
    #region Variables
    public float speed;
    public float sprintModifier;
    public float jumpForce;
    public float lengthOfSlide;
    public float slideModifier;

    private float baseFOV;
    private float sprintFOVModifier = 1.5f;
    private float movementCounter;
    private float idleCounter;
    private float slide_time;

    public int max_health;
    private int current_health;

    public Camera normalCam;

    public GameObject cameraParent;

    public Transform weaponParent;
    public Transform groundDetector;
    private Transform ui_healthbar;

    private Text ui_ammo;

    public LayerMask ground;

    public Rigidbody rig;

    private Vector3 weaponParentOrigin;
    private Vector3 targetWeaponBobPosition;
    private Vector3 slide_dir;
    private Vector3 origin;
    private Vector3 weaponParentCurrentPosition;

    private Manager manager;

    private Weapon weapon;

    private bool sliding;
    
    #endregion

    #region MonoBehaviour Callbacks
    private void Start()
    {
        manager = GameObject.Find("Manager").GetComponent<Manager>();
        weapon = GetComponent<Weapon>();
        current_health = max_health;

        cameraParent.SetActive(photonView.IsMine);
        if (!photonView.IsMine)
        {
            gameObject.layer = 11;
        }

        baseFOV = normalCam.fieldOfView;
        origin = normalCam.transform.localPosition;

        if(Camera.main) Camera.main.enabled = false;

        rig = GetComponent<Rigidbody>();
        weaponParentOrigin = weaponParent.localPosition;
        weaponParentCurrentPosition = weaponParentOrigin;

        if (photonView.IsMine)
        {
            ui_healthbar = GameObject.Find("HUD/Health/Bar").transform;
            ui_ammo = GameObject.Find("HUD/Ammo/Text").GetComponent<Text>();
            RefreshHealthBar();
        }
    }
    // Update is called once per frame
    private void Update()
    {
        if (!photonView.IsMine) return;
        //Input
        float t_hmove = Input.GetAxisRaw("Horizontal");
        float t_vmove = Input.GetAxisRaw("Vertical");

        //Controls
        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool jump = Input.GetKey(KeyCode.Space);

        //States
        bool isGrounded = Physics.Raycast(groundDetector.position, Vector3.down, 0.1f, ground);
        bool isJumping = jump && isGrounded;
        bool isSprinting = sprint && t_vmove > 0 && !isJumping && isGrounded;

        //Jumping
        if (isJumping)
        {
            rig.AddForce(Vector3.up * jumpForce);
        }
 
        if (Input.GetKeyDown(KeyCode.Delete)) TakeDamage(100);

        //Head Bob
        if (sliding) { }
        else if (t_hmove == 0 && t_vmove == 0)
        {
            HeadBob(idleCounter, 0.025f, 0.025f);
            idleCounter += Time.deltaTime;
            weaponParent.localPosition = Vector3.Lerp(weaponParent.localPosition, targetWeaponBobPosition, Time.deltaTime * 2f);
        }

        else if (!isSprinting)
        {
            HeadBob(movementCounter, 0.035f, 0.035f);
            movementCounter += Time.deltaTime * 3f;
            weaponParent.localPosition = Vector3.Lerp(weaponParent.localPosition, targetWeaponBobPosition, Time.deltaTime * 6f);
        }
        else
        {
            HeadBob(movementCounter, 0.15f, 0.075f);
            movementCounter += Time.deltaTime * 7f;
            weaponParent.localPosition = Vector3.Lerp(weaponParent.localPosition, targetWeaponBobPosition, Time.deltaTime * 10f);
        }

        //UI Refreshes
        RefreshHealthBar();
        weapon.RefreshAmmo(ui_ammo);

        
    }

    // FixedUpdate is called once per fixed rate
    void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        //Input
        float t_hmove = Input.GetAxisRaw("Horizontal");
        float t_vmove = Input.GetAxisRaw("Vertical");

        //Controls
        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool jump = Input.GetKey(KeyCode.Space);
        bool slide = Input.GetKey(KeyCode.LeftControl);

        //States
        bool isGrounded = Physics.Raycast(groundDetector.position, Vector3.down, 0.1f, ground);
        bool isJumping = jump && isGrounded;
        bool isSprinting = sprint && t_vmove > 0 && !isJumping && isGrounded;
        bool isSliding = isSprinting && slide && !sliding;




        //Movement
        Vector3 t_direction = Vector3.zero;
        float t_adjustedSpeed = speed;
        if (!sliding)
        {
            t_direction = new Vector3(t_hmove, 0, t_vmove);
            t_direction.Normalize();
            t_direction = transform.TransformDirection(t_direction);

            if (isSprinting) t_adjustedSpeed *= sprintModifier;
        }
        else
        {
            t_direction = slide_dir;
            t_adjustedSpeed *= slideModifier;
            slide_time -= Time.deltaTime;
            if(slide_time <= 0)
            {
                sliding = false;
                normalCam.transform.localPosition += Vector3.up * 0.5f;
            }
        }

        Vector3 t_targetVelocity = t_direction * t_adjustedSpeed * Time.deltaTime;
        t_targetVelocity.y = rig.velocity.y;
        rig.velocity = t_targetVelocity;

        //Sliding
        if (isSliding)
        {
            sliding = true;
            slide_dir = t_direction;
            slide_time = lengthOfSlide;

            //adjust camera
            weaponParentCurrentPosition += Vector3.down * 0.5f;

        }


        //Camera stuff
        if (sliding)
        {
            Mathf.Lerp(normalCam.fieldOfView, baseFOV * sprintFOVModifier * 1.25f, Time.deltaTime * 8f);
            normalCam.transform.localPosition = Vector3.Lerp(normalCam.transform.localPosition, origin + Vector3.down * 0.5f, Time.deltaTime * 6f);
        }
        else {
            if (isSprinting)
            {
                normalCam.fieldOfView = Mathf.Lerp(normalCam.fieldOfView, baseFOV * sprintFOVModifier, Time.deltaTime * 8f);
            }
            else
            {
                normalCam.fieldOfView = Mathf.Lerp(normalCam.fieldOfView, baseFOV, Time.deltaTime * 8f);
            }

            normalCam.transform.localPosition = Vector3.Lerp(normalCam.transform.localPosition, origin, Time.deltaTime * 6f);
        }

    }
    #endregion

    #region Private Methods
    void HeadBob(float p_z, float p_x_intensity, float p_y_intensity)
    {
        targetWeaponBobPosition = weaponParentCurrentPosition + new Vector3(Mathf.Cos(p_z) * p_x_intensity, Mathf.Sin(p_z * 2) * p_y_intensity, 0);
    }

    void RefreshHealthBar()
    {
        float t_health_ratio = (float)current_health / (float)max_health;
        ui_healthbar.localScale = Vector3.Lerp(ui_healthbar.localScale, new Vector3(t_health_ratio, 1, 1), Time.deltaTime * 8f);
    }

    #endregion

    #region Public Methods
    
    public void TakeDamage(int p_damage)
    {
        if (photonView.IsMine)
        {
            current_health -= p_damage;
            RefreshHealthBar();
            Debug.Log(current_health);

            if(current_health <= 0)
            {
                manager.Spawn();
                PhotonNetwork.Destroy(gameObject);
            }
        }
    }
    #endregion
}
