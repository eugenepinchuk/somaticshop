import {Component, Input, OnInit} from '@angular/core';
import {AppConstants} from '../../../../../app-constants';
import {ProductCard} from '../../../../../viewModels/ProductCard';

@Component({
  selector: 'app-product-card-item',
  templateUrl: './product-card-item.component.html',
  styleUrls: ['./product-card-item.component.scss']
})
export class ProductCardItemComponent implements OnInit {

  @Input()
  card: ProductCard;

  constructor() {
  }

  ngOnInit(): void {
  }

  get currency(): string {
    return AppConstants.CURRENCY;
  }
}
