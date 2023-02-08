using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller : MonoBehaviour
{
    public GameObject seed;
    public bool falling = false;
    public float speed = 0f;
    Camera main;
    public GameObject textureUpdater;
    private bool planted;
    public GameObject musicPlayer;
    private bool playing;
    private float initalHeight;
    private Vector3 initialPos;

    void Awake() {
        main = Camera.main;
        initalHeight = main.transform.position.y;
        initialPos = seed.transform.position;
    }

    // Update is called once per frame
    void Update() {
        if (falling) {
            if (seed.transform.position.y > 5) {
                seed.transform.Rotate(Vector3.forward * 50f * Time.deltaTime);
                seed.transform.position += Vector3.down * speed * Time.deltaTime;
                main.transform.position += Vector3.down * speed * Time.deltaTime;
                speed += Time.deltaTime * 0.5f;
            } else if (!planted) {
                main.GetComponent<TextureGenerator>().SeedPlant(
                    main.WorldToScreenPoint(seed.transform.position)
                );
                planted = true;
            } else if (speed > 0) {
                speed -= Time.deltaTime * 1.1f;
                main.transform.position += Vector3.down * speed * Time.deltaTime;
            }
        } else {
            seed.transform.position = 
            new Vector3(
                main.ScreenToWorldPoint(Vector3.right * Input.mousePosition.x).x ,
                initialPos.y,
                initialPos.z);
        }

        if (Input.GetMouseButton(0)) {
            falling = true;
            if (!playing) {
                musicPlayer.GetComponent<AudioSource>().time = 0.6f;
                musicPlayer.GetComponent<AudioSource>().Play();
                playing = true;
            }
        }

    }

    public void Reset() {
        falling = false;
        StartCoroutine(flyBack());
    }

    public IEnumerator flyBack() {
        speed = 0f;
        speed -= Time.deltaTime * 1.1f;
        float halfHeight = initalHeight / 2f;

        while (main.transform.position.y < halfHeight) {
            speed -= Time.deltaTime * 1.1f;
            main.transform.position += Vector3.down * speed * Time.deltaTime;
            
            yield return new WaitForEndOfFrame();
        }

        while (speed < 0f) {
            speed += Time.deltaTime * 1.1f;
            main.transform.position += Vector3.down * speed * Time.deltaTime;
            
            yield return new WaitForEndOfFrame();
        }

        falling = false;
        speed = 0f;
        main.transform.position = new Vector3(main.transform.position.x, initalHeight, main.transform.position.z);
        planted = false;
    }

}
