using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Visuales")]
    public Sprite activeSprite; // Sprite cuando ya lo tocaste (ej. antorcha encendida)
    public Sprite inactiveSprite; // Sprite por defecto
    private SpriteRenderer sr;

    private bool isActive = false;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (inactiveSprite != null) sr.sprite = inactiveSprite;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !isActive)
        {
            ActivateCheckpoint();
        }
    }

    void ActivateCheckpoint()
    {
        isActive = true;

        // 1. Cambiar visualmente
        if (sr != null && activeSprite != null)
        {
            sr.sprite = activeSprite;
        }

        // 2. Avisar al GameManager
        if (GameManager01.instance != null)
        {
            GameManager01.instance.UpdateCheckPoint(transform.position);
        }

        Debug.Log("Checkpoint! ");

        // Opcional: Sonido o partículas aquí
    }
}