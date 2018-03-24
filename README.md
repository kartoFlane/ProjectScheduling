# ProjectScheduling

Implementation of a genetic algorithm used to solve [Multi-Skill Resource-Constrained Project Scheduling Problems (MS-RCPSP)](http://imopse.ii.pwr.wroc.pl/psp_problem.html).

The program is specifically tailored to work on benchmark datasets provided by [iMOPSE](http://imopse.ii.pwr.wroc.pl/index.html) project at Wrocław University of Technology.

## Summary

This project explores solving of MS-RCPSP using a genetic algorithm. Implementation includes different strategies for crossover, mutation, clone elimination, and specimen selection, which were individually investigated at various stages. Base implementation uses penalties to score instances, although a penalty-less solution was briefly pursued on a separate branch.

This program reaches correct solutions for all entries in the dataset. For some of the entries, it reaches slightly better results than achieved by researches, while for others, it struggles exceptionally and gets stuck on local minima.
