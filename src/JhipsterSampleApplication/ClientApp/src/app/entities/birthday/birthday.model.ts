export interface IBirthday {
  id?: string;
  name?: string;
  date?: Date;
  description?: string;
}

export class Birthday implements IBirthday {
  constructor(
    public id?: string,
    public name?: string,
    public date?: Date,
    public description?: string,
  ) {}
}
