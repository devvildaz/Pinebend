using UnityEngine;
namespace Pinebend {
public class TimingGate : MonoBehaviour {
 public int number;
 void OnTriggerEnter(Collider c){if(c.attachedRigidbody && c.attachedRigidbody.TryGetComponent<TestCar>(out var car))car.Gate(number);}
}
}
