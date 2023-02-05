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

    void Awake() {
        main = Camera.main;
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
                seed.transform.position.y,
                seed.transform.position.z);
        }

        if (Input.GetMouseButton(0)) {
            falling = true;
            if (!playing) {
                musicPlayer.GetComponent<AudioSource>().time = 0.8f;
                musicPlayer.GetComponent<AudioSource>().Play();
                playing = true;
            }
        }
    }
}
