using UnityEngine;

namespace RaftGame.Crew
{
    public enum CrewActivity { Idle, Walking, Constructing, WaitingForAccess, Gathering, WaitingForMaterials, WaitingForSalvage, Fishing, Cooking, Eating, Purifying, Drinking }

    /// <summary>Procedural presentation only. CrewManager owns movement, tasks and progress.</summary>
    public sealed class CrewView : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private GameObject hammer;
        [SerializeField] private Transform progressRoot;
        [SerializeField] private Transform progressFill;
        [SerializeField] private GameObject fishingRod;
        [SerializeField] private Transform rodTip;
        [SerializeField] private LineRenderer fishingLine;
        [SerializeField] private Transform bobber;
        [SerializeField] private GameObject cookingTool;
        [SerializeField] private GameObject mealFood;
        public GameObject DrinkCup;
        private Camera sceneCamera;
        private float animationTime;
        private Vector3 bodyRest;

        public void Initialize(Camera camera)
        {
            sceneCamera = camera;
            bodyRest = body.localPosition;
            Present(CrewActivity.Idle, 0f, 0f, Vector3.zero);
        }

        public void Present(CrewActivity activity, float progress, float deltaTime, Vector3 workLocation)
        {
            animationTime += Mathf.Max(0f, deltaTime);
            bool walking = activity == CrewActivity.Walking;
            bool working = activity == CrewActivity.Constructing;
            bool gathering = activity == CrewActivity.Gathering;
            bool fishing = activity == CrewActivity.Fishing;
            bool cooking = activity == CrewActivity.Cooking || activity == CrewActivity.Purifying;
            bool eating = activity == CrewActivity.Eating || activity == CrewActivity.Drinking;
            float stride = walking ? Mathf.Sin(animationTime * 10f) * 28f : 0f;
            leftLeg.localRotation = Quaternion.Euler(stride, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-stride, 0, 0);
            leftArm.localRotation = Quaternion.Euler(fishing ? -28f : walking ? -stride * 0.65f : 0f, 0, -6f);
            rightArm.localRotation = Quaternion.Euler(working ? -65f + Mathf.Sin(animationTime * 12f) * 32f :
                gathering ? -50f + Mathf.Sin(animationTime * 6f) * 18f : fishing ? -55f + Mathf.Sin(animationTime * 1.5f) * 2f :
                cooking ? -55f + Mathf.Sin(animationTime * 4f) * 12f : eating ? -100f + Mathf.Sin(animationTime * 4f) * 12f : stride * 0.65f, 0, 6f);
            body.localPosition = bodyRest + Vector3.up * (walking ? Mathf.Abs(Mathf.Sin(animationTime * 10f)) * 0.025f : Mathf.Sin(animationTime * 2f) * 0.008f);
            hammer.SetActive(working);
            if (cookingTool != null) cookingTool.SetActive(activity == CrewActivity.Cooking);
            if (mealFood != null) mealFood.SetActive(activity == CrewActivity.Eating);
            if (DrinkCup != null) DrinkCup.SetActive(activity == CrewActivity.Drinking);
            if (fishingRod != null) fishingRod.SetActive(fishing);
            if (fishingLine != null) fishingLine.gameObject.SetActive(fishing);
            if (bobber != null) bobber.gameObject.SetActive(fishing);
            if (fishing && rodTip != null && bobber != null && fishingLine != null)
            {
                bobber.position = workLocation + Vector3.up * (Mathf.Sin(animationTime * 2f) * 0.025f);
                fishingLine.SetPosition(0, rodTip.position);
                fishingLine.SetPosition(1, Vector3.Lerp(rodTip.position, bobber.position, 0.5f) + Vector3.down * 0.13f);
                fishingLine.SetPosition(2, bobber.position);
            }
            progressRoot.gameObject.SetActive(working || gathering || fishing || cooking || eating);
            if (working || gathering || fishing || cooking || eating)
            {
                progress = Mathf.Clamp01(progress);
                progressRoot.position = workLocation + Vector3.up * 0.85f;
                if (sceneCamera != null) progressRoot.rotation = sceneCamera.transform.rotation;
                progressFill.localScale = new Vector3(0.96f * progress, 0.09f, 0.025f);
                progressFill.localPosition = new Vector3(-0.48f + 0.48f * progress, 0, -0.025f);
            }
        }
    }
}
