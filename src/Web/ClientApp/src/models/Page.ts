export class Page<T> {
  constructor(
    public items?: T[],
    public pageNumber?: number,
    public totalPages?: number,
    public totalItems?: number,
    public hasPreviousPage?: boolean,
    public hasNextPage?: boolean) {
  }
}
