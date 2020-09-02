import {Inject, Injectable} from '@angular/core';
import {HttpClient, HttpParams} from '@angular/common/http';
import {Observable, of} from 'rxjs';
import {Catalog} from '../models/catalog/Catalog';
import {Page} from '../models/Page';
import {ICatalogImage} from '../interfaces/ICatalogImage';
import {map} from 'rxjs/operators';
import {serialize} from 'object-to-formdata';
import {Product} from '../models/product/Product';
import {PriceRange} from '../models/PriceRange';
import {Brand} from '../models/Brand';

@Injectable({
  providedIn: 'root'
})
export class CatalogService {

  constructor(
    private httpClient: HttpClient,
    @Inject('BASE_URL') private baseUrl: string) {
  }

  public roots(): Observable<Catalog[]> {
    return this.httpClient.get<Catalog[]>(`${this.baseUrl}api/catalogs`);
  }

  public childsFor(catalogId: number | null | undefined, depth: number | null | undefined = null): Observable<Catalog[]> {
    const params = new HttpParams().set('depth', String(depth));
    return this.httpClient
      .get<Catalog[]>(`${this.baseUrl}api/catalogs/${catalogId ? catalogId + '/' : ''}childs`, {params: params});
  }

  public getCatalogsPage(page: number, sortTitle: string = null, search: string = null): Observable<Page<Catalog>> {
    let params = new HttpParams();
    params = params.set('page', String(page));
    if (sortTitle) {
      params = params.set('sortTitle', sortTitle);
    }
    if (search) {
      params = params.set('search', search);
    }
    return this.httpClient.get<Page<Catalog>>(`${this.baseUrl}api/catalogs`, {params: params});
  }

  public getCatalogProductsPage(
    catalogId: number | null | undefined,
    page: number,
    priceRange?: PriceRange,
    brandIds?: number[],
    sort?: Map<string, string>,
    specs?: Map<string, string[]>): Observable<Page<Product>> {

    let params = new HttpParams();
    params = params.set('page', String(page));

    sort?.forEach((value, key) => params = params.append(`sort.${key}`, value));
    brandIds?.forEach(bId => params = params.append(`brand`, String(bId)));

    if (specs) {
      let i = 0;
      specs.forEach((values, key) => {
        values?.forEach((value, idx) => {
          params = params.append(`spec[${i + idx}].name`, key);
          params = params.append(`spec[${i + idx}].value`, value);
        });
        i += values.length;
      });
    }

    if (priceRange) {
      params = params.append(`price.from`, String(priceRange.from));
      params = params.append(`price.to`, String(priceRange.to));
    }

    return this.httpClient
      .get<Page<Product>>(`${this.baseUrl}api/catalogs/${catalogId ? catalogId + '/' : ''}products`, {params: params});
  }

  public brandsFor(catalogId: number | null | undefined): Observable<Brand[]> {
    return this.httpClient.get<Brand[]>(`${this.baseUrl}api/catalogs/${catalogId ? catalogId + '/' : ''}brands`);
  }

  public priceRangeFor(catalogId: number | null | undefined): Observable<PriceRange> {
    return this.httpClient.get<PriceRange>(`${this.baseUrl}api/catalogs/${catalogId ? catalogId + '/' : ''}priceRange`);
  }

  public specificationsFor(catalogId: number | null | undefined): Observable<{ name: string, values: string[] }[]> {
    return this.httpClient
      .get<{ name: string, values: string[] }[]>(`${this.baseUrl}api/catalogs/${catalogId ? catalogId + '/' : ''}specifications`);
  }

  public hasProducts(catalogId: number | null | undefined): Observable<boolean> {
    return this.httpClient.get<boolean>(`${this.baseUrl}api/catalogs/${catalogId ? catalogId + '/' : ''}hasProducts`);
  }

  public parentsFor(id: number, includeSelf = false): Observable<Catalog[]> {
    return this.httpClient.get<Catalog[]>(`${this.baseUrl}api/catalogs/${id}/parents?includeSelf=${includeSelf}`);
  }

  public catalogById(id: number): Observable<Catalog> {
    if (!id) {
      return of(null);
    }
    return this.httpClient.get<Catalog>(`${this.baseUrl}api/catalogs/${id}`);
  }

  public getCatalogImage(catalogId: number): Observable<ICatalogImage> {
    return this.httpClient.get<ICatalogImage>(`${this.baseUrl}api/catalogs/${catalogId}/image`);
  }

  public getCatalogImageUrl(catalogId: number): Observable<string> {
    return this.httpClient.get<ICatalogImage>(`${this.baseUrl}api/catalogs/${catalogId}/image`)
      .pipe(map(catalogImage => catalogImage ? `${this.baseUrl}api/files/${catalogImage.fileId}` : null));
  }

  public createCatalog(catalog: Catalog, imageFile: File): Observable<Catalog> {

    const formData = new FormData();
    serialize(catalog, null, formData, 'catalog');
    formData.set('image', imageFile);

    return this.httpClient.post<Catalog>(`${this.baseUrl}api/catalogs`, formData);
  }

  public updateCatalog(catalog: Catalog, imageFile: File): Observable<Catalog> {

    const formData = new FormData();
    serialize(catalog, null, formData, 'catalog');
    formData.set('image', imageFile);

    return this.httpClient.put<Catalog>(`${this.baseUrl}api/catalogs`, formData);
  }

  public deleteCatalogs(catalogIds: number[]): Observable<any> {
    let params = new HttpParams();
    catalogIds.forEach(id => params = params.append('catalogId', String(id)));
    return this.httpClient.delete(`${this.baseUrl}api/catalogs`, {params: params});
  }
}
