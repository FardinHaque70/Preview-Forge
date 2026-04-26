#if UNITY_EDITOR
namespace FardinHaque.ImprovedAssetTools.Editor
{
	public enum PreviewAssetTypeKey
	{
		ModelPrefab,
		SpritePrefab,
		UiPrefab,
		ParticlePrefab,
		Material,
		NonVisualPrefab
	}

	public enum ModelPreviewDefaultVisualMode
	{
		None,
		Normals,
		UvChecker,
		VertexColor,
		Overdraw
	}

	public enum ParticlePreviewDefaultMotionShape
	{
		Circle,
		Line,
		Figure8
	}

	public enum MaterialPreviewDefaultMeshMode
	{
		PipelineDefault,
		Sphere,
		Cube,
		Torus,
		Quad
	}
}
#endif
