using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon.StructWrapping;
using Photon.Pun;
using UnityEngine;

public class PlayerController : MonoBehaviourPunCallbacks
{
    public Transform viewPoint;
    public float mouseSensitivity = 1f;
    private float verticalRotationStore;
    private Vector2 mouseInput;
    public bool invertLook = false;

    public float moveSpeed = 5f, runSpeed = 8f;
    private float activeMoveSpeed;
    private Vector3 moveDir, movement;

    public CharacterController characterController;

    private Camera cam;
    public float jumForce = 12f, gravityMod = 2.5f;

    public Transform groundCheckPoint;
    private bool isGrounded;
    public LayerMask groundLayer;

    public GameObject bulletImpact;
    //public float timeBetweenShots = 0.1f;
    private float shotCounter;

    public float maxHeatValue = 10f, /*heatPerShot = 1f,*/ coolRate = 4f, overHeatCoolRate = 5f;
    private float heatCounter;
    private bool overHeated;

    public Gun[] allGuns;
    private int selectedGun = 0;
    public float muzzleDisplayTime;
    private float muzzleCounter;

    public GameObject playerHitImpact;

    public int maxHealth = 100;
    private int currentHealth;

    public Animator anim;

    public GameObject playerModel;

    public Transform modelGunPoint;
    public Transform gunHolder;

    public Material[] allSkins;
    public float adsSpeed = 5f;

    public Transform adsOutPoint, adsInPoint;

    public AudioSource footStepSlow, footStopFast;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        cam = Camera.main;

        UIController.instance.WeaponTempSlider.maxValue = maxHeatValue;

        // SwitchGun();
        photonView.RPC("SetGun", RpcTarget.All, selectedGun);

        currentHealth = maxHealth;

        if (photonView.IsMine)
        {
            playerModel.SetActive(false);
            UIController.instance.HealthSlider.maxValue = maxHealth;
            UIController.instance.HealthSlider.value = currentHealth;
        }
        else
        {
            gunHolder.parent = modelGunPoint;
            gunHolder.localPosition = Vector3.zero;
            gunHolder.localRotation = Quaternion.identity;
        }

        playerModel.GetComponent<Renderer>().material = allSkins[photonView.Owner.ActorNumber % allSkins.Length];
    }

    // Update is called once per frame
    void Update()
    {
        if (photonView.IsMine)
        {
            // Movement
            mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * mouseSensitivity;

            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y + mouseInput.x, transform.rotation.eulerAngles.z);

            verticalRotationStore += mouseInput.y;
            verticalRotationStore = Mathf.Clamp(verticalRotationStore, -60f, 60f);

            if (invertLook)
            {
                viewPoint.rotation = Quaternion.Euler(verticalRotationStore, viewPoint.rotation.eulerAngles.y, viewPoint.rotation.eulerAngles.z);
            }
            else
            {
                viewPoint.rotation = Quaternion.Euler(-verticalRotationStore, viewPoint.rotation.eulerAngles.y, viewPoint.rotation.eulerAngles.z);
            }

            moveDir = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

            if (Input.GetKey(KeyCode.LeftShift))
            {
                activeMoveSpeed = runSpeed;
                if (!footStopFast.isPlaying && moveDir != Vector3.zero) {
                    footStopFast.Play();
                    footStepSlow.Stop();
                }
            }
            else
            {
                activeMoveSpeed = moveSpeed;
                if (!footStepSlow.isPlaying && moveDir != Vector3.zero) {
                    footStopFast.Stop();
                    footStepSlow.Play();
                }
            }

            if (moveDir == Vector3.zero || !isGrounded) {
                footStepSlow.Stop();
                footStopFast.Stop();
            }

            float yVel = movement.y;
            movement = ((transform.forward * moveDir.z) + (transform.right * moveDir.x)).normalized * activeMoveSpeed;

            movement.y = yVel;
            if (characterController.isGrounded)
            {
                movement.y = 0f;
            }

            isGrounded = Physics.Raycast(groundCheckPoint.position, Vector3.down, 0.25f, groundLayer);

            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                movement.y = jumForce;
            }

            movement.y += Physics.gravity.y * Time.deltaTime * gravityMod;

            characterController.Move(Time.deltaTime * movement);

            // Weapon overheating and shooting

            if (allGuns[selectedGun].muzzleFlash.activeInHierarchy)
            {
                muzzleCounter -= Time.deltaTime;
                if (muzzleCounter <= 0)
                {
                    allGuns[selectedGun].muzzleFlash.SetActive(false);
                }
            }

            if (!overHeated)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Shoot();
                }

                if (Input.GetMouseButton(0) && allGuns[selectedGun].isAutomatic)
                {
                    shotCounter -= Time.deltaTime;

                    if (shotCounter <= 0)
                    {
                        Shoot();
                    }
                }

                heatCounter -= coolRate * Time.deltaTime;
            }
            else
            {
                heatCounter -= overHeatCoolRate * Time.deltaTime;
                if (heatCounter <= 0)
                {
                    overHeated = false;

                    UIController.instance.overheatedMessage.gameObject.SetActive(false);
                }
            }

            if (heatCounter < 0)
            {
                heatCounter = 0;
            }
            UIController.instance.WeaponTempSlider.value = heatCounter;

            // Gun Selection
            if (Input.GetAxisRaw("Mouse ScrollWheel") > 0)
            {
                selectedGun++;

                if (selectedGun >= allGuns.Length)
                {
                    selectedGun = 0;
                }
                // SwitchGun();
                photonView.RPC("SetGun", RpcTarget.All, selectedGun);
            }
            else if (Input.GetAxisRaw("Mouse ScrollWheel") < 0)
            {
                selectedGun--;

                if (selectedGun < 0)
                {
                    selectedGun = allGuns.Length - 1;
                }
                // SwitchGun();
                photonView.RPC("SetGun", RpcTarget.All, selectedGun);
            }

            for (int i = 0; i < allGuns.Length; i++)
            {
                if (Input.GetKeyDown((i + 1).ToString()))
                {
                    selectedGun = i;
                    // SwitchGun();
                    photonView.RPC("SetGun", RpcTarget.All, selectedGun);
                }
            }

            //Animation
            anim.SetBool("grounded", isGrounded);
            anim.SetFloat("speed", moveDir.magnitude);

            //ADS Setting

            if (Input.GetMouseButton(1)) {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, allGuns[selectedGun].adsZoom, adsSpeed * Time.deltaTime);
                gunHolder.position = Vector3.Lerp(gunHolder.position, adsInPoint.position, adsSpeed * Time.deltaTime);
            } else {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f, adsSpeed * Time.deltaTime);
                gunHolder.position = Vector3.Lerp(gunHolder.position, adsOutPoint.position, adsSpeed * Time.deltaTime);
            }

            // Cursor Locking
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
            }
            else if (Cursor.lockState == CursorLockMode.None)
            {
                if (Input.GetMouseButtonDown(0) && !UIController.instance.optionsScreen.activeInHierarchy)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
        }
    }

    private void Shoot()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        ray.origin = cam.transform.position;

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject.tag == "Player")
            {
                Debug.Log("Hit : " + hit.collider.gameObject.GetPhotonView().Owner.NickName);
                PhotonNetwork.Instantiate(playerHitImpact.name, hit.point, Quaternion.identity);
                hit.collider.gameObject.GetPhotonView().RPC("DealDamage", RpcTarget.All, photonView.Owner.NickName, allGuns[selectedGun].shotDamage, PhotonNetwork.LocalPlayer.ActorNumber);
            }
            else
            {
                GameObject bulletImpactObject = Instantiate(bulletImpact, hit.point + (hit.normal * 0.002f), Quaternion.LookRotation(hit.normal, Vector3.up));

                Destroy(bulletImpactObject, 2f);
            }
        }

        shotCounter = allGuns[selectedGun].timeBetweenShots;

        heatCounter += allGuns[selectedGun].heatPerShot;

        if (heatCounter >= maxHeatValue)
        {
            heatCounter = maxHeatValue;
            overHeated = true;

            UIController.instance.overheatedMessage.gameObject.SetActive(true);
        }

        allGuns[selectedGun].muzzleFlash.SetActive(true);
        muzzleCounter = muzzleDisplayTime;

        allGuns[selectedGun].shotSound.Stop();
        allGuns[selectedGun].shotSound.Play();
    }

    [PunRPC]
    public void DealDamage(string damager, int damageAmount, int actor)
    {
        TakeDamage(damager, damageAmount, actor);
    }

    public void TakeDamage(string damager, int damageAmount, int actor)
    {
        if (photonView.IsMine)
        {
            currentHealth -= damageAmount;

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                PlayerSpawner.instance.Die(damager);
                MatchManager.instance.UpdateStatsSend(actor, 0, 1);
            }

            UIController.instance.HealthSlider.value = currentHealth;
        }
    }

    private void SwitchGun()
    {
        foreach (Gun gun in allGuns)
        {
            gun.gameObject.SetActive(false);
        }
        allGuns[selectedGun].gameObject.SetActive(true);
        allGuns[selectedGun].muzzleFlash.SetActive(false);
    }

    [PunRPC]
    public void SetGun(int gunToSwitchTo)
    {
        if (gunToSwitchTo < allGuns.Length)
        {
            selectedGun = gunToSwitchTo;
            SwitchGun();
        }
    }

    private void LateUpdate()
    {
        if (photonView.IsMine)
        {
            if (MatchManager.instance.state == MatchManager.GameState.Playing)
            {
                cam.transform.position = viewPoint.position;
                cam.transform.rotation = viewPoint.rotation;
            }
            else
            {
                cam.transform.position = MatchManager.instance.mapCamPoint.position;
                cam.transform.rotation = MatchManager.instance.mapCamPoint.rotation;
            }
        }
    }
}
