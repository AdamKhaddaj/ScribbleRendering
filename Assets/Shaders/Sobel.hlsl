#ifndef SOBELOUTLINES_INCLUDED
#define SOBELOUTLINES_INCLUDED

// Adjacency of pixels used during post-processing
static float2 sobelPixels[9] = {
	float2(-1, 1), float2(0, 1), float2(1,1),
	float2(-1, 0), float2(0,0), float2(1, 0),
	float2(-1, -1), float2(0,-1), float2(1, -1)
};

// Convolution matrix in the x direction, will find vertical borders
static float convolutionX[9] = {
	1, 0 ,-1,
	2, 0, -2,
	1, 0, -1
};

// Convolution matrix in the y direction, will find horizontal borders
static float convolutionY[9] = {
	1, 2, 1,
	0, 0, 0,
	-1, -2, -1
};

// This runs the sobel filter on the depth buffer (technically the camera depth texture) 
void DepthSobel_float(float2 UV, float Thickness, out float Out) {
	float2 sobel = 0;

	for (int i = 0; i < 9; i++) {
		float depth = SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV + sobelPixels[i] * Thickness);
		sobel += depth * float2(convolutionX[i], convolutionY[i]);
	}
	
	Out = length(sobel);
}

// This runs the sobel filter on the colour values on screen (this isn't being used since it doesn't look great with hatching)
void ColourSobel_float(float2 UV, float Thickness, out float Out) {
	float2 sobelR = 0;
	float2 sobelG = 0;
	float2 sobelB = 0;

	for (int i = 0; i < 9; i++) {
		float3 colour = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV + sobelPixels[i] * Thickness);
		sobelR += colour.r * float2(convolutionX[i], convolutionY[i]);
		sobelG += colour.g * float2(convolutionX[i], convolutionY[i]);
		sobelB += colour.b * float2(convolutionX[i], convolutionY[i]);

	}

	float2 temp = max(length(sobelR), length(sobelG));
	Out = max(temp, sobelB);
}

// This runs the sobel filter on the normal buffer (has varying results, kind of depends on the 3D models you use, usually it looks good though)
void NormalSobel_float(float2 UV, float Thickness, out float Out) {
	float2 sobelR = 0;
	float2 sobelG = 0;
	float2 sobelB = 0;

	for (int i = 0; i < 9; i++) {
		float3 colour = SHADERGRAPH_SAMPLE_SCENE_NORMAL(UV + sobelPixels[i] * Thickness);
		sobelR += colour.r * float2(convolutionX[i], convolutionY[i]);
		sobelG += colour.g * float2(convolutionX[i], convolutionY[i]);
		sobelB += colour.b * float2(convolutionX[i], convolutionY[i]);

	}

	float2 temp = max(length(sobelR), length(sobelG));
	Out = max(temp, sobelB);
}

#endif