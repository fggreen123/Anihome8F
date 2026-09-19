using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Intro : MonoBehaviour
{
    [SerializeField] private RawImage IntroImage;
    [SerializeField] RawImage IntroLogo;
    [SerializeField] private RawImage Noise;

    void Start()
    {
        Noise.enabled = true;
        StartCoroutine(StartIntro());
    }
    void Update()
    {
        
    }
    IEnumerator StartIntro()
    {
        IntroImage.enabled = false;
        IntroLogo.color = new Color(255, 255, 255, 0);
        Noise.GetComponent<AudioSource>().Play();
        Noise.GetComponent<AudioSource>().volume = 0;
        while (IntroLogo.rectTransform.localScale.x < 1.58f)
        {
            Noise.GetComponent<AudioSource>().volume += 0.01f;
            IntroLogo.rectTransform.localScale += new Vector3(0.01f,0.01f);
            IntroLogo.color += new Color(255, 255, 255, 0.01f);
            yield return new WaitForSeconds(0.1f);
        }
        
        IntroLogo.color = new Color(255, 255, 255, 0);
        yield return new WaitForSeconds(1f);
        IntroImage.GetComponent<AudioSource>().Play();
        IntroImage.enabled = true;
        yield return new WaitForSeconds(5f);
        IntroImage.GetComponent<AudioSource>().Play();
        IntroImage.enabled = false;
        yield return new WaitForSeconds(0.9f);
        IntroImage.GetComponent<AudioSource>().Play();
        Noise.enabled = false;
        yield return new WaitForSeconds(0.4f);
        //씬 로드는 메인화면으로 수정예정.
        SceneManager.LoadScene("DormitoryFloor");
    }
}
