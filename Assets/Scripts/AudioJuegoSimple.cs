using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class AudioJuegoSimple : MonoBehaviour
{
    public enum EstadoMusica { Nivel, SoloAmbiente, Jefe, Final }
    private static AudioJuegoSimple instancia;
    public static AudioJuegoSimple Existente => instancia;
    public static AudioJuegoSimple Instancia
    {
        get
        {
            if (instancia == null && Application.isPlaying)
                instancia = FindFirstObjectByType<AudioJuegoSimple>();
            if (instancia == null && Application.isPlaying)
                instancia = new GameObject("Audio_Juego").AddComponent<AudioJuegoSimple>();
            return instancia;
        }
    }

    [Header("Volumen general")]
    [Range(0f, 1f)] public float volumen = 0.65f;
    [Header("Efectos existentes")]
    public AudioClip taladroLoop;
    public AudioClip disparo;
    [Tooltip("Sonido AL DISPARAR el canon. La explosion va en su propio campo.")]
    public AudioClip canon;
    public AudioClip impacto;
    public AudioClip derrumbe;
    public AudioClip victoria;
    [Header("Efectos nuevos")]
    public AudioClip explosionCanon;
    public AudioClip motorExcavadora;
    public AudioClip gusanoEscupir;
    public AudioClip gusanoDanio;
    public AudioClip temblor;
    [Header("Ambiente y musica")]
    public AudioClip ambienteCueva;
    public AudioClip musicaNivel;
    public AudioClip musicaJefe;
    [Min(0f)] public float fundidoMusica = 0.6f;
    [Header("Mezcla de volumenes")]
    [Range(0f, 1f)] public float volumenEfectos = 1f;
    [Range(0f, 1f)] public float volumenTaladro = 0.28f;
    [Range(0f, 1f)] public float volumenMotor = 0.3f;
    [Range(0f, 1f)] public float volumenAmbiente = 0.4f;
    [Range(0f, 1f)] public float volumenMusicaNivel = 0.45f;
    [Range(0f, 1f)] public float volumenMusicaJefe = 0.55f;
    [Tooltip("Antes del jefe se oye el ambiente sin el motor en reposo.")]
    public bool silenciarMotorEnAntesala = true;
    [Header("Evitar saturacion por sonidos repetidos")]
    [Min(0f)] public float intervaloSonidoDanioGusano = 0.15f;
    [Min(0f)] public float intervaloSonidoTemblor = 0.5f;

    public EstadoMusica EstadoActual { get; private set; } = EstadoMusica.Nivel;
    private AudioSource efectos;
    private AudioSource fuenteTaladro;
    private AudioSource fuenteMotor;
    private AudioSource ambiente;
    private AudioSource musica;
    private Coroutine cambioMusica;
    private float factorMusica = 1f;
    private float fuerzaMotor;
    private bool finalTerminado;
    private float ultimoDerrumbe = -10f;
    private float ultimoDanioGusano = -10f;
    private float ultimoTemblor = -10f;
    private bool iniciado;

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        instancia = this;
        if (taladroLoop == null) taladroLoop = Cargar("TaladroLoop");
        if (disparo == null) disparo = Cargar("Disparo");
        if (canon == null) canon = Cargar("Canon");
        if (impacto == null) impacto = Cargar("Impacto");
        if (derrumbe == null) derrumbe = Cargar("Derrumbe");
        if (victoria == null) victoria = Cargar("Victoria");
        if (explosionCanon == null) explosionCanon = Cargar("ExplosionCanon");
        if (motorExcavadora == null) motorExcavadora = Cargar("MotorExcavadora");
        if (gusanoEscupir == null) gusanoEscupir = Cargar("GusanoEscupir");
        if (gusanoDanio == null) gusanoDanio = Cargar("GusanoDanio");
        if (temblor == null) temblor = Cargar("Temblor");
        if (ambienteCueva == null) ambienteCueva = Cargar("AmbienteCueva");
        if (musicaNivel == null) musicaNivel = Cargar("MusicaNivel");
        if (musicaJefe == null) musicaJefe = Cargar("MusicaJefe");
        efectos = CrearFuente(false);
        fuenteTaladro = CrearFuente(true);
        fuenteMotor = CrearFuente(true);
        ambiente = CrearFuente(true);
        musica = CrearFuente(true);
        ActualizarVolumenes();
    }

    private void Start()
    {
        if (instancia != this)
            return;
        iniciado = true;
        PrepararAmbiente();
        EstablecerMusica(EstadoActual, true);
    }

    private void OnEnable()
    {
        if (!iniciado || instancia != this)
            return;
        PrepararAmbiente();
        EstablecerMusica(EstadoActual, true);
    }

    private AudioSource CrearFuente(bool bucle)
    {
        AudioSource fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.loop = bucle;
        return fuente;
    }

    private static AudioClip Cargar(string nombre) =>
        Resources.Load<AudioClip>("SonidosCierre/" + nombre);

    private void Update()
    {
        ActualizarVolumenes();
    }

    private void ActualizarVolumenes()
    {
        if (fuenteTaladro != null) fuenteTaladro.volume = volumen * volumenTaladro;
        if (fuenteMotor != null) fuenteMotor.volume = volumen * volumenMotor * fuerzaMotor;
        if (ambiente != null) ambiente.volume = volumen * volumenAmbiente;
        if (musica != null)
            musica.volume = volumen * factorMusica *
                (musica.clip != null && musica.clip == musicaJefe ? volumenMusicaJefe : volumenMusicaNivel);
    }

    private void Tocar(AudioClip clip, float fuerza)
    {
        if (isActiveAndEnabled && clip != null && efectos != null)
            efectos.PlayOneShot(clip, volumen * volumenEfectos * fuerza);
    }

    public void Disparo() => Tocar(disparo, 0.45f);
    public void Canon() => Tocar(canon, 0.8f);
    public void ExplosionCanon() => Tocar(explosionCanon, 0.9f);
    public void Impacto() => Tocar(impacto, 0.65f);
    public void GusanoEscupir() => Tocar(gusanoEscupir, 0.8f);

    public void GusanoDanio()
    {
        if (Time.unscaledTime - ultimoDanioGusano < intervaloSonidoDanioGusano)
            return;
        ultimoDanioGusano = Time.unscaledTime;
        Tocar(gusanoDanio, 0.65f);
    }

    public void Temblor()
    {
        if (Time.unscaledTime - ultimoTemblor < intervaloSonidoTemblor)
            return;
        ultimoTemblor = Time.unscaledTime;
        Tocar(temblor, 0.7f);
    }

    public void Derrumbe()
    {
        if (Time.unscaledTime - ultimoDerrumbe < 0.4f)
            return;
        ultimoDerrumbe = Time.unscaledTime;
        Tocar(derrumbe, 0.6f);
    }

    public void Victoria()
    {
        finalTerminado = true;
        SostenerTaladro(false);
        SostenerMotor(false, 0f);
        Tocar(victoria, 0.8f);
    }

    public void SostenerTaladro(bool activo)
    {
        ActualizarBucle(fuenteTaladro, taladroLoop, activo && !finalTerminado);
        ActualizarVolumenes();
    }

    public void SostenerMotor(bool activo, float velocidadNormalizada)
    {
        float movimiento = Mathf.Clamp01(velocidadNormalizada);
        fuerzaMotor = Mathf.Lerp(0.5f, 1f, movimiento);
        if (fuenteMotor != null) fuenteMotor.pitch = Mathf.Lerp(0.8f, 1.25f, movimiento);
        bool antesala = silenciarMotorEnAntesala && EstadoActual == EstadoMusica.SoloAmbiente;
        ActualizarBucle(fuenteMotor, motorExcavadora, activo && !antesala && !finalTerminado);
        ActualizarVolumenes();
    }

    private void ActualizarBucle(AudioSource fuente, AudioClip clip, bool activo)
    {
        if (fuente == null)
            return;
        if (!activo || !isActiveAndEnabled || clip == null)
        {
            if (fuente.isPlaying) fuente.Stop();
            return;
        }
        if (fuente.clip != clip)
        {
            fuente.Stop();
            fuente.clip = clip;
        }
        if (!fuente.isPlaying) fuente.Play();
    }

    private void PrepararAmbiente() => ActualizarBucle(ambiente, ambienteCueva, true);

    public void MusicaDeNivel() => EstablecerMusica(EstadoMusica.Nivel);
    public void SoloAmbiente() => EstablecerMusica(EstadoMusica.SoloAmbiente, true);
    public void MusicaDelJefe() => EstablecerMusica(EstadoMusica.Jefe);
    public void IniciarFinal() => EstablecerMusica(EstadoMusica.Final);

    public void EstablecerMusica(EstadoMusica estado, bool inmediato = false)
    {
        EstadoActual = estado;
        finalTerminado = false;
        if (cambioMusica != null)
        {
            StopCoroutine(cambioMusica);
            cambioMusica = null;
        }
        if (musica == null || !isActiveAndEnabled)
            return;
        AudioClip pista = estado == EstadoMusica.Nivel ? musicaNivel :
            estado == EstadoMusica.Jefe ? musicaJefe : null;
        if (inmediato)
        {
            musica.Stop();
            musica.clip = pista;
            factorMusica = pista != null ? 1f : 0f;
            ActualizarVolumenes();
            if (pista != null) musica.Play();
            return;
        }
        cambioMusica = StartCoroutine(CambiarMusica(pista));
    }

    private IEnumerator CambiarMusica(AudioClip pista)
    {
        float duracion = Mathf.Max(0f, fundidoMusica);
        float origen = factorMusica;
        if (musica.isPlaying && duracion > 0f)
        {
            for (float t = 0f; t < duracion; t += Time.unscaledDeltaTime)
            {
                factorMusica = Mathf.Lerp(origen, 0f, t / duracion);
                ActualizarVolumenes();
                yield return null;
            }
        }
        musica.Stop();
        musica.clip = pista;
        factorMusica = 0f;
        if (pista != null)
        {
            ActualizarVolumenes();
            musica.Play();
            if (duracion > 0f)
                for (float t = 0f; t < duracion; t += Time.unscaledDeltaTime)
                {
                    factorMusica = Mathf.Clamp01(t / duracion);
                    ActualizarVolumenes();
                    yield return null;
                }
            factorMusica = 1f;
        }
        ActualizarVolumenes();
        cambioMusica = null;
    }

    public static void DetenerTaladroSiExiste()
    {
        if (instancia != null) instancia.SostenerTaladro(false);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        cambioMusica = null;
        if (fuenteTaladro != null) fuenteTaladro.Stop();
        if (fuenteMotor != null) fuenteMotor.Stop();
        if (ambiente != null) ambiente.Stop();
        if (musica != null) musica.Stop();
        if (efectos != null) efectos.Stop();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }
}
