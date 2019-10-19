using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Valve.VR.Extras;

public class DLA : MonoBehaviour {
  // State
  private bool isPaused = false;
  private enum Modes {Point, Rings, Circles, Spheres, Blanket, Line, Wave};
  private Modes currentMode = Modes.Spheres;

  // Mesh
  private GameObject meshContainer;
  private MeshCollider mesh;
  private Vector3 meshOrigin;
  private Vector3 meshSize;

  // Walkers
  private List<GameObject> activeWalkers = new List<GameObject>();
  private List<GameObject> inactiveWalkers = new List<GameObject>();
  List<GameObject> walkersToRemove;
  private float walkerSize = .01f;
  private int maxWalkerAge = 10000;
  private float maxWalkerDistance = 100f;
  private float walkerSpeed = .01f;
  private float jitterAmount = .0036f;
  private float minHeight = 1f;
  private float maxHeight = 2f;
  private float minRadius = .01f;
  private float maxRadius = .06f;
  private int spawnInterval = 30;
  private int lastSpawnTime = 0;
  private bool isSpawning = true;

  // Materials
  public Material walkerMaterial;
  public Material aggregateMaterial;

  // SteamVR laser pointer
  public SteamVR_LaserPointer laserPointer;
  private bool laserIsHovering = false;


  //===========================================================
  //  Main setup
  //===========================================================

  void Start() {
    // Get the mesh
    meshContainer = GameObject.FindWithTag("MeshContainer");
    mesh = GameObject.FindWithTag("Mesh").GetComponent<MeshCollider>();

    // Mesh information
    meshOrigin = new Vector3(
      meshContainer.transform.position.x,
      meshContainer.transform.position.y,
      meshContainer.transform.position.z
    );

    meshSize = new Vector3(
      mesh.bounds.size.x,
      mesh.bounds.size.y,
      mesh.bounds.size.z
    );
  }


  //===========================================================
  //  Main update loop
  //===========================================================

  void Update() {
    // Reset simulation with 'r'
    if(Input.GetKeyDown("r")) {
      Reset();
    }

    // Toggle pause using Space
    if(Input.GetKeyDown(KeyCode.Space)) {
      isPaused = !isPaused;
    }

    // Number keys to change mode
    if(Input.GetKeyDown("1")) { currentMode = Modes.Point; spawnInterval = 2; }
    if(Input.GetKeyDown("2")) { currentMode = Modes.Rings; spawnInterval = 20;  }
    if(Input.GetKeyDown("3")) { currentMode = Modes.Circles; spawnInterval = 20; }
    if(Input.GetKeyDown("4")) { currentMode = Modes.Spheres; spawnInterval = 20; }
    if(Input.GetKeyDown("5")) { currentMode = Modes.Blanket; spawnInterval = 100; }
    if(Input.GetKeyDown("6")) { currentMode = Modes.Line;    }
    if(Input.GetKeyDown("7")) { currentMode = Modes.Wave;    }

    if(!isPaused) {
      // Spawn walkers at the point where the laser pointer ray meets the active meshes
      if(isSpawning && laserIsHovering) {
        if(Time.frameCount >= lastSpawnTime + spawnInterval) {
          RaycastHit hit;
          Ray ray = new Ray(laserPointer.transform.position, laserPointer.transform.forward);
          bool bHit = Physics.Raycast(ray, out hit);

          if(bHit) {
            SpawnWalkersAbove(hit.point);
          }

          lastSpawnTime = Time.frameCount;
        }
      }

      walkersToRemove = new List<GameObject>();

      foreach(GameObject walker in activeWalkers) {
        // Handle movement of all active walkers
        if(walker.layer == LayerMask.NameToLayer("Walkers")) {
          WalkerScript ws = walker.GetComponent<WalkerScript>();

          // Flag for removal any walkers that are too old or too far away from the mesh
          if(ws.age > maxWalkerAge || Vector3.Distance(walker.transform.position, meshContainer.transform.position) > maxWalkerDistance) {
            walkersToRemove.Add(walker);

          // Move all active walkers
          } else {
            // Move towards origin of mesh
            // walker.transform.position = Vector3.MoveTowards(walker.transform.position, meshContainer.transform.position + targetOffset + Random.insideUnitSphere * 10f, 1.0f *  Time.deltaTime);

            // Move walkers forward
            // walker.transform.position += new Vector3(0,0,walkerSpeed);

            // Move walkers downward
            walker.transform.position += new Vector3(0,-walkerSpeed,0);

            // Add motion jitter
            walker.transform.position += Random.insideUnitSphere * jitterAmount;
          }
        } else if(walker.layer == LayerMask.NameToLayer("Aggregated")) {
          walker.GetComponent<Renderer>().material = aggregateMaterial;
        }
      }

      // Remove walkers that have been flagged for removal
      foreach(GameObject walker in walkersToRemove) {
        inactiveWalkers.Add(walker);
        activeWalkers.Remove(walker);
      }
    }
  }


  //===========================================================
  //  Reset simulation by removing all walkers
  //===========================================================

  void Reset() {
    foreach(GameObject walker in activeWalkers) {
      Destroy(walker);
    }

    foreach(GameObject walker in inactiveWalkers) {
      Destroy(walker);
    }

    activeWalkers = new List<GameObject>();
    inactiveWalkers = new List<GameObject>();
  }


  //===========================================================
  //  Abstraction function to use appropriate walker
  //  spawn functions based on current mode
  //===========================================================
  void SpawnWalkersAbove(Vector3 point) {
    switch(currentMode) {
      // Spawn single walker
      case Modes.Point:
        SpawnWalkerAt(
          point.x,
          minHeight,
          point.z
        );

        break;

      // Spawn walkers in rings
      case Modes.Rings:
        SpawnWalkersInRing(
          point.x,
          minHeight,
          point.z,
          Random.Range(minRadius, maxRadius)
        );

        break;

      // Spawn walkers in circles (filled)
      case Modes.Circles:
        SpawnWalkersInCircle(
          point.x,
          minHeight,
          point.z,
          Random.Range(minRadius, maxRadius)
        );

        break;

      // Spawn walkers in spheres
      case Modes.Spheres:
        SpawnWalkersInSphere(
          point.x,
          minHeight,
          point.z,
          minRadius + (maxRadius-minRadius)/2
        );

        break;

      // Spawn walkers along a straight line
      case Modes.Line:
        SpawnWalkersAlongLine();
        break;

      // Spawn walkers along a sine wave
      case Modes.Wave:
        SpawnWalkersAlongWave();
        break;

      // Spawn walkers evenly along the entire mesh area
      case Modes.Blanket:
        SpawnWalkerBlanket();
        break;
    }
  }


  //===========================================================
  //  Functions for spawning walkers in specific layouts
  //===========================================================

  // Create walkers along the edge of a circle
  void SpawnWalkersInRing(float centerX, float centerY, float centerZ, float radius) {
    float circumference = Mathf.PI * (radius * 2);
    int maxPoints = Mathf.FloorToInt(circumference / walkerSize);
    int numPoints = Mathf.FloorToInt(maxPoints * .75f);
    // int numPoints = Random.Range(5,maxPoints);
    // int numPoints = 100;

    for(var i=1; i<=numPoints; i++) {
      float angle = (360f / numPoints) * i;
      float x = centerX + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
      float z = centerZ + radius * Mathf.Sin(angle * Mathf.Deg2Rad);

      SpawnWalkerAt(x, centerY, z);
    }
  }

  // Create walkers within a circle
  void SpawnWalkersInCircle(float centerX, float centerY, float centerZ, float radius) {
    // int numPoints = Random.Range(10, Mathf.FloorToInt(radius*10));
    int numPoints = 200;

    for(var i=1; i<=numPoints; i++) {
      Vector2 coords = Random.insideUnitCircle * radius * 2;

      SpawnWalkerAt(
        centerX + coords.x,
        centerY,
        centerZ + coords.y
      );
    }
  }

  // Create walkers randomly within a sphere
  void SpawnWalkersInSphere(float centerX, float centerY, float centerZ, float radius) {
    // int numPoints = Random.Range(10, Mathf.FloorToInt(radius*10));
    int numPoints = 50;

    for(var i=1; i<=numPoints; i++) {
      Vector3 coords = Random.insideUnitSphere * radius * 2;

      SpawnWalkerAt(
        centerX + coords.x,
        centerY + coords.y,
        centerZ + coords.z
      );
    }
  }

  // Create walkers along a straight line
  void SpawnWalkersAlongLine() {
    int maxNumPoints = Mathf.FloorToInt(meshSize.x / walkerSize);
    // int numPoints = maxNumPoints;
    int numPoints = 1000;

    for(var i=0; i<numPoints; i++) {
      // SpawnWalkerAt(i * walkerSize, 2f, meshSizeZ/2);  // straight line, evenly spaced walkers

      // Range fan alone X axis
      SpawnWalkerAt(
        Random.Range(meshOrigin.x - meshSize.x/2, meshOrigin.x + meshSize.x/2),
        Random.Range(minHeight, maxHeight),
        meshOrigin.z
      );
    }
  }

  // Create walkers along a sine wave
  void SpawnWalkersAlongWave() {
    int numPeriods = 2;
    float amplitude = .2f;
    int numPoints = 2000;

    for(int i=0; i<numPoints; i++) {
      float x = Random.Range(meshOrigin.x - meshSize.x/2, meshOrigin.x + meshSize.x/2);
      float z = Mathf.Sin(
        map(
          x,
          meshOrigin.x - meshSize.x/2,
          meshOrigin.x + meshSize.x/2,
          0f,
          360f * numPeriods
        ) * Mathf.Deg2Rad
      ) * amplitude;

      SpawnWalkerAt(
        x,
        Random.Range(minHeight, maxHeight),
        meshOrigin.z + z
      );
    }
  }

  // Create walkers evenly across entire surface of mesh
  void SpawnWalkerBlanket() {
    int numPoints = 1000;

    float xStart = Random.Range(-meshSize.x/2, (meshSize.x/2) * .75f);
    float xEnd = Random.Range(xStart, meshSize.x);

    float zStart = Random.Range(-meshSize.z/2, (meshSize.z/2) * .75f);
    float zEnd = Random.Range(zStart, meshSize.z);

    for(int i=0; i<numPoints; i++) {
      /********
        Rock7
      *********/
      // walker.transform.position = Random.insideUnitSphere * walkerCloudDiameter + walkerOffset;
      // walker.transform.position = new Vector3(Random.Range(-5f, 5f), Random.Range(0,0), Random.Range(-10f,-5f));
      // walker.transform.position = new Vector3(Random.Range(0,9f), Random.Range(-4f,8f), Random.Range(-20f,3f));

      /********
        Log01
      *********/
      // Front - entire log
      // SpawnWalkerAt(
      //   Random.Range(0, meshSizeX),
      //   Random.Range(0, meshSizeY),
      //   Random.Range(-2f, -10f)
      // );

      // Top - entire log
      // SpawnWalkerAt(
      //   Random.Range(0, meshSizeX),
      //   Random.Range(minHeight, maxHeight),
      //   Random.Range(0, meshSizeZ)
      // );

      // Top - patch
      SpawnWalkerAt(
        meshOrigin.x + Random.Range(xStart, xEnd),
        Random.Range(minHeight, maxHeight),
        meshOrigin.z + Random.Range(zStart, zEnd)
      );
    }
  }


  //===========================================================
  //
  //===========================================================

  // Create a single walker at a given position
  void SpawnWalkerAt(float x, float y, float z) {
    GameObject walker = CreateWalker();

    walker.transform.position = new Vector3(x, y, z);

    activeWalkers.Add(walker);
  }


  //===========================================================
  //
  //===========================================================

  // Create and set up a generic walker with a Rigidbody, material, WalkerScript, and other configs
  GameObject CreateWalker() {
    // Create and configure a sphere for the walker
    GameObject walker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    walker.transform.localScale = new Vector3(walkerSize, walkerSize, walkerSize);
    walker.layer = LayerMask.NameToLayer("Walkers");

    // Apply the assigned walker material and turn off shadows
    Renderer r = walker.GetComponent<Renderer>();
    r.material = walkerMaterial;
    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    r.receiveShadows = false;

    // Attach the collision detection script
    walker.AddComponent<WalkerScript>();

    // Create and configure Rigidbody
    Rigidbody rb = walker.gameObject.AddComponent<Rigidbody>();
    rb.transform.localScale = new Vector3(walkerSize, walkerSize, walkerSize);
    rb.useGravity = false;
    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    rb.constraints = RigidbodyConstraints.FreezeAll;

    return walker;
  }


  //===========================================================
  //  Utilities
  //===========================================================

  // Map a number from one range to another
  float map(float s, float a1, float a2, float b1, float b2) {
    return b1 + (s-a1)*(b2-b1)/(a2-a1);
  }


  //===========================================================
  //  SteamVR inputs
  //===========================================================

  void Awake() {
    laserPointer.PointerClick += PointerClick;
    laserPointer.PointerIn += PointerInside;
    laserPointer.PointerOut += PointerOutside;
  }

  public void PointerClick(object sender, PointerEventArgs e) {
    // if(
    //   e.target.name == "Log01" ||
    //   e.target.name == "Rock2" ||
    //   e.target.name == "Rock4" ||
    //   e.target.name == "Rock7" ||
    //   e.target.name == "Rock10"
    // ) {
    //   isSpawning = !isSpawning;
    // }
  }

  public void PointerInside(object sender, PointerEventArgs e) {
    if(
      e.target.name == "Log01" ||
      e.target.name == "Rock2" ||
      e.target.name == "Rock4" ||
      e.target.name == "Rock7" ||
      e.target.name == "Rock10"
    ) {
      laserIsHovering = true;
    }
  }

  public void PointerOutside(object sender, PointerEventArgs e) {
    if(
      e.target.name == "Log01" ||
      e.target.name == "Rock2" ||
      e.target.name == "Rock4" ||
      e.target.name == "Rock7" ||
      e.target.name == "Rock10"
    ) {
      laserIsHovering = false;
    }
  }
}
