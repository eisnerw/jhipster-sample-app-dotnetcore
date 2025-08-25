export interface IMovie {
  id?: string;
  title?: string;
  release_year?: number;
  genres?: string[];
  runtime_minutes?: number;
  country?: string;
  languages?: string[];
  budget_usd?: number;
  gross_usd?: number;
  rotten_tomatoes_scores?: number;
}

export class Movie implements IMovie {
  constructor(
    public id?: string,
    public title?: string,
    public release_year?: number,
    public genres?: string[],
    public runtime_minutes?: number,
    public country?: string,
    public languages?: string[],
    public budget_usd?: number,
    public gross_usd?: number,
    public rotten_tomatoes_scores?: number,
  ) {}
}

export interface IViewResult {
  categoryName: string;
  count: number;
}
