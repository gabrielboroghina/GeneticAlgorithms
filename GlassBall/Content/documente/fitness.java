//introduceti functia fitness 
public static int fitness(int i){
	//functia performanta - calculeaza costul total al deplasarii
	//pentru un individ din populatie (adica a unui traseu)
	int s,j;
	s=0;
	for(j=2;j<=n;j++)
		s+=a[pop[i][j-1]][pop[i][j]];
	return s;
	}
