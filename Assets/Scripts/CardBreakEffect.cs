using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CardBreakEffect : MonoBehaviour
{
    void Awake()
    {
        StartCoroutine(BreakEffectCoroutine());
    }
    IEnumerator BreakEffectCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }
}
