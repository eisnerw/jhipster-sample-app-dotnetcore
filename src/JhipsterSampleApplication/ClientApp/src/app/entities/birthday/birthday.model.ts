export interface IBirthday {
  id?: string;
  lname?: string;
  fname?: string;
  sign?: string;
  dob?: Date;
  isAlive?: boolean;
}

export class Birthday implements IBirthday {
  constructor(
    public id?: string,
    public lname?: string,
    public fname?: string,
    public sign?: string,
    public dob?: Date,
    public isAlive?: boolean,
  ) {}
}

export interface IViewResult {
  categoryName: string;
  count: number;
}
