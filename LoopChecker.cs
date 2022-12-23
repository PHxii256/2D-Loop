using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityTimer;

public class LoopChecker : MonoBehaviour
{                                                                //NOTE: for performance the max generated with these values is about 50 points averaging around 30.
	[SerializeField] private float addCheckTime = 0.02f;         //adds a waypoint and checks for a loop 
	[SerializeField] private float removeEveryTime = 0.1f;       //remove first added point from the loop "it despawns"
	[SerializeField] private float minDistBetweenWaypoints = 1f; //to avoid have a lot of points in a small area making the line appear to be deswpawning slower
	[SerializeField] private float AreaVisablityTime = 1f;
	[SerializeField] private LineRenderer lineConnect;
	[SerializeField] private SpriteShapeController spriteController;
	[SerializeField] private SpriteShapeRenderer shapeRenderer;

	private Queue<Vector3> waypoints = new Queue<Vector3>();
	private LineRenderer lineRenderer;
	private Vector3[] points;
	private Player playerInstance;
	private Transform player;
	private Timer[] timers = new Timer[3];

	private void Awake()
    {
		lineRenderer = gameObject.GetComponent<LineRenderer>();
		player = GetComponentInParent<Transform>();
		playerInstance = GetComponentInParent<Player>();
		DeathUI.ResetLevel += RestartAllTimers;

		shapeRenderer.enabled = false;
		spriteController.enabled = false;
	}

    private void OnDestroy()
    {
		DeathUI.ResetLevel -= RestartAllTimers;
	}

    private void StartTimers()
    {
		timers[0] = Timer.Register(addCheckTime, AddWaypoint, isLooped: true);
		timers[1] = Timer.Register(addCheckTime, CheckWaypoints, isLooped: true);
		timers[2] = Timer.Register(removeEveryTime, RemoveWaypoint, isLooped: true);
	}

    void Update()
    {
		if (player)
		{
			points = waypoints.ToArray();
			if (waypoints.Count != 0) VisualizeLines();
		}

		else foreach (Timer timer in timers) timer.Cancel();
	}

	public void RestartAllTimers() => StartTimers();

	public void CancelAllTimers() 
	{
	    foreach (Timer timer in timers) timer.Cancel();
		waypoints.Clear();
		lineRenderer.positionCount = 0;
		lineConnect.positionCount = 0;
		DisableArea();
	}

	private void AddWaypoint() 
	{
		if (!player) return;
		if (waypoints.Count != 0)
		{
			if (Vector3.Distance(points[points.Length -1], player.position) >= minDistBetweenWaypoints)
			{
				waypoints.Enqueue(new Vector2(player.position.x, player.position.y));
			}
		}

		else waypoints.Enqueue(new Vector2(player.position.x, player.position.y));
	}

	private void CheckWaypoints()
	{
		if (points.Length < 4 || !player) return;

		for (int i = 0; i < points.Length - 3; i++)
		{
			if (AreLinesIntersecting(points[points.Length - 1], points[points.Length - 2], points[i], points[i + 1], false)) //loop found, add logic here
			{
				GenerateArea();
				Timer.Register(AreaVisablityTime, DisableArea);
				waypoints.Clear();
			}
		}
	}

	private void RemoveWaypoint()
	{
		if(waypoints.Count != 0) waypoints.Dequeue();
	}

	private void GenerateArea() 
	{
		if (!player) return;
		Spline spline = spriteController.spline;
		List<Collider2D> collidersInside = new List<Collider2D>();
		Vector2[] points2d = new Vector2[points.Length];

		EnableArea();

		for (int i = 0; i < points.Length; i++) //fixes an issue with generation of collider
        {
			points2d[i] = new Vector2(points[i].x, points[i].y);
        }
		spriteController.polygonCollider.points = points2d;

		ContactFilter2D filter = new ContactFilter2D().NoFilter();
		spriteController.polygonCollider.OverlapCollider(filter, collidersInside);

        foreach (Collider2D collider in collidersInside)
        {
			if (collider.GetComponent<EnemyHealth>()) collider.GetComponent<EnemyHealth>().ReceiveDamage(playerInstance.damage); //damage enemies inside the loop
		}

		for (int i = 0; i < points.Length; i++)
        {
			spline.InsertPointAt(i, points[i]);
			spline.SetTangentMode(i, ShapeTangentMode.Continuous);
			spline.SetHeight(i, 0);
		}

	}

	private void DisableArea() 
	{
		Spline spline = spriteController.spline;
		spline.Clear();
		shapeRenderer.enabled = false;
		spriteController.enabled = false;
		spriteController.polygonCollider.enabled = false;
	}

	private void EnableArea()
	{
		shapeRenderer.enabled = true;
		spriteController.enabled = true;
		spriteController.polygonCollider.enabled = true;
	}

	private void VisualizeLines() 
	{
		if (points.Length < 2) return;

		lineRenderer.positionCount = points.Length;

		for (int i = points.Length - 1; i > 0; i--)
        {
			lineRenderer.SetPosition(i, points[i]);
			lineRenderer.SetPosition(i - 1, points[i - 1]);
		}

		lineConnect.positionCount = 2;
		lineConnect.SetPosition(1, player.position);
		lineConnect.SetPosition(0, points[points.Length - 1]);
	}

	private void DebugLines() //for inspector use only if needed
	{
        for (int i = 0; i < points.Length - 2; i++) 
        {
            Debug.DrawLine(points[i], points[i + 1], Color.red);
        }

        lineRenderer.SetPosition(0, player.position);
    }

	public static bool AreLinesIntersecting(Vector2 l1_p1, Vector2 l1_p2, Vector2 l2_p1, Vector2 l2_p2, bool shouldIncludeEndPoints)
	{
		//To avoid floating point precision issues we can add a small value
		float epsilon = 0.001f;

		bool isIntersecting = false;

		float denominator = (l2_p2.y - l2_p1.y) * (l1_p2.x - l1_p1.x) - (l2_p2.x - l2_p1.x) * (l1_p2.y - l1_p1.y);

		//Make sure the denominator is > 0, if not the lines are parallel
		if (denominator != 0f)
		{
			float u_a = ((l2_p2.x - l2_p1.x) * (l1_p1.y - l2_p1.y) - (l2_p2.y - l2_p1.y) * (l1_p1.x - l2_p1.x)) / denominator;
			float u_b = ((l1_p2.x - l1_p1.x) * (l1_p1.y - l2_p1.y) - (l1_p2.y - l1_p1.y) * (l1_p1.x - l2_p1.x)) / denominator;

			//Are the line segments intersecting if the end points are the same
			if (shouldIncludeEndPoints)
			{
				//Is intersecting if u_a and u_b are between 0 and 1 or exactly 0 or 1
				if (u_a >= 0f + epsilon && u_a <= 1f - epsilon && u_b >= 0f + epsilon && u_b <= 1f - epsilon)
				{
					isIntersecting = true;
				}
			}
			else
			{
				//Is intersecting if u_a and u_b are between 0 and 1
				if (u_a > 0f + epsilon && u_a < 1f - epsilon && u_b > 0f + epsilon && u_b < 1f - epsilon)
				{
					isIntersecting = true;
				}
			}
		}

		return isIntersecting;
	}
}