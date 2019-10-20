using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WalkerScript : MonoBehaviour
{
  public int age = 0;

  // Start is called before the first frame update
  void Start() {
  }

  // Update is called once per frame
  void Update() {
    if(gameObject.layer == LayerMask.NameToLayer("Walkers")) {
      age++;
    }
  }

  void OnCollisionEnter(Collision collision) {
    if(
      collision.gameObject.layer == LayerMask.NameToLayer("Mesh") ||
      collision.gameObject.layer == LayerMask.NameToLayer("Aggregated")
    ) {
      gameObject.layer = LayerMask.NameToLayer("Aggregated");

      Rigidbody rb = GetComponent<Rigidbody>();

      if(rb) {
        Destroy(rb);
      }
    }
  }
}
