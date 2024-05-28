#ifndef RANGE_INCLUDED
#define RANGE_INCLUDED


void GradientLight_float(float lightVal, float maxVal, float rate, float base, out float Out) {
	Out = 1 - pow(base, rate * (lightVal - maxVal));
}

#endif