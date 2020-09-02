﻿using AutoMapper;
using Core;
using Core.Common;
using Core.Common.Sorting;
using Core.Dto;
using Core.Entities;
using Core.Exceptions;
using Core.Services;
using Core.Specifications.BrandSpecs;
using Core.Specifications.CatalogSpecs;
using Core.Specifications.ProductSpecs;
using IdentityServer4.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Web.Models;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CatalogsController : ControllerBase
    {

        private readonly CatalogService _catalogService;
        private readonly ProductService _productService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CatalogsController> _logger;
        private readonly IMapper _mapper;

        public CatalogsController(
            CatalogService catalogService,
            ProductService productService,
            IUnitOfWork unitOfWork,
            ILogger<CatalogsController> logger,
            IMapper mapper)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        [HttpGet("{catalogId}")]
        public async Task<ActionResult<CatalogDto>> GetById(int catalogId)
        {
            var catalog = await _unitOfWork.CatalogRepository.FindByIdAsync(catalogId);
            return _mapper.Map<CatalogDto>(catalog);
        }

        [HttpGet("{catalogId?}/products")]
        public async Task<IActionResult> CatalogProducts(
           [FromRoute] int? catalogId,
           [FromQuery] int? page = null,
           [FromQuery(Name = "price")] PriceRangeModel priceRange = null,
           [FromQuery(Name = "brand")] int[] brandIds = null,
           [FromQuery(Name = "sort")] SortModel sort = null,
           [FromQuery(Name = "spec")] List<SpecificationModel> specs = null,
           [FromQuery] int pageSize = 10)
        {

            var catalogs = await GetChildsWithSelfRecursive(catalogId);
            if (catalogs.IsNullOrEmpty())
            {
                return NoContent();
            }

            var spec = new ProductFilterSpec();

            if (!sort.IsNullOrEmpty())
            {
                foreach (var item in sort)
                {
                    if (Enum.TryParse<OrderBy>(item.Value, true, out var order))
                    {
                        if (item.Key.Equals(nameof(Product.Title), StringComparison.OrdinalIgnoreCase))
                        {
                            if (order == OrderBy.ASC) { spec.Query.OrderBy(a => a.Title); }
                            else if (order == OrderBy.DESC) { spec.Query.OrderByDescending(a => a.Title); }
                        }
                        if (item.Key.Equals(nameof(Product.Price), StringComparison.OrdinalIgnoreCase))
                        {
                            if (order == OrderBy.ASC) { spec.Query.OrderBy(a => a.Price); }
                            else if (order == OrderBy.DESC) { spec.Query.OrderByDescending(a => a.Price); }
                        }
                        if (item.Key.Equals(nameof(Product.Date), StringComparison.OrdinalIgnoreCase))
                        {
                            if (order == OrderBy.ASC) { spec.Query.OrderBy(a => a.Date); }
                            else if (order == OrderBy.DESC) { spec.Query.OrderByDescending(a => a.Date); }
                        }
                    }
                }
            }

            var lambdaCombiner = new LambdaExpressionCombiner<Product>();

            var spec1 = new ProductByCatalogIdSpec(catalogs.Select(a => a.Id).ToArray());
            lambdaCombiner.Add(spec1.WhereExpressions.First());

            if (!brandIds.IsNullOrEmpty())
            {
                var spec2 = new ProductByBrandIdSpec(brandIds);
                lambdaCombiner.Add(spec2.WhereExpressions.First());
            }

            if (priceRange != null && (priceRange.From.HasValue || priceRange.To.HasValue))
            {
                lambdaCombiner.Add(new ProductByPriceRangeSpec(priceRange.From, priceRange.To).WhereExpressions.First());
            }

            if (!specs.IsNullOrEmpty())
            {
                lambdaCombiner.Add(new ProductBySpecsSpec(specs.Select(a => new KeyValuePair<string, string>(a.Name, a.Value)).ToArray()).WhereExpressions.First());
            }

            var lambda = lambdaCombiner.Combine(ExpressionType.AndAlso);

            if (lambda != null)
            {
                spec.Query.Where(lambda);
            }

            if (page.HasValue)
            {
                spec.Query.Paginate((page.Value - 1) * pageSize, pageSize);

                var totalCount = 0;

                if (spec.WhereExpressions.IsNullOrEmpty())
                {
                    totalCount = await _unitOfWork.ProductRepository.GetAllCountAsync();
                }
                else
                {
                    var noPagingSpec = new ProductFilterSpec();
                    spec.Query.Where(lambda);
                    totalCount = await _unitOfWork.ProductRepository.GetBySpecCountAsync(noPagingSpec);
                }

                var items = _mapper.Map<IEnumerable<ProductDto>>(await _unitOfWork.ProductRepository.GetBySpecAsync(spec));
                var paginatedList = new Page<ProductDto>(items, totalCount, page.Value, pageSize);

                return Ok(paginatedList);
            }

            return Ok(_mapper.Map<IEnumerable<ProductDto>>(await _unitOfWork.ProductRepository.GetBySpecAsync(spec)));
        }

        [HttpGet("products")]
        public async Task<IActionResult> CatalogProducts(
           [FromQuery] int? page = null,
           [FromQuery(Name = "price")] PriceRangeModel priceRange = null,
           [FromQuery(Name = "brand")] int[] brandIds = null,
           [FromQuery(Name = "sort")] SortModel sort = null,
           [FromQuery(Name = "spec")] List<SpecificationModel> specs = null,
           [FromQuery] int pageSize = 10) => await CatalogProducts(null, page, priceRange, brandIds, sort, specs, pageSize);

        [HttpGet("{catalogId?}/brands")]
        public async Task<ActionResult<IEnumerable<BrandDto>>> CatalogBrands([FromRoute] int? catalogId)
        {
            var catalogs = await GetChildsWithSelfRecursive(catalogId);
            if (catalogs.IsNullOrEmpty())
            {
                return NoContent();
            }

            var spec = new BrandsByCatalogIdSpec(catalogs.Select(c => c?.Id).ToArray());
            return Ok(_mapper.Map<IEnumerable<BrandDto>>(await _unitOfWork.BrandRepository.GetBySpecAsync(spec)));
        }

        [HttpGet("brands")]
        public async Task<ActionResult<IEnumerable<BrandDto>>> CatalogBrands() => await CatalogBrands(null);


        [HttpGet("{catalogId?}/priceRange")]
        public async Task<ActionResult<PriceRangeModel>> CatalogPriceRange([FromRoute] int? catalogId)
        {
            var catalogs = await GetChildsWithSelfRecursive(catalogId);
            if (catalogs.IsNullOrEmpty())
            {
                return NoContent();
            }

            var products = await _unitOfWork.ProductRepository.GetBySpecAsync(
                new ProductByCatalogIdSpec(catalogs.Select(a => a.Id).ToArray()));

            if (products.IsNullOrEmpty())
            {
                return NoContent();
            }

            return new PriceRangeModel()
            {
                From = products.Select(a => a.Price)?.Min(),
                To = products.Select(a => a.Price)?.Max()
            };
        }
        [HttpGet("priceRange")]
        public async Task<ActionResult<PriceRangeModel>> CatalogPriceRange()
            => await CatalogPriceRange(null);

        [HttpGet("{catalogId?}/specifications")]
        public async Task<IActionResult> Specifications([FromRoute] int? catalogId)
        {
            var catalogs = await GetChildsWithSelfRecursive(catalogId);
            if (catalogs.IsNullOrEmpty())
            {
                return NoContent();
            }

            var spec = new ProductSpecsByCatalogIdSpec(catalogs.Select(a => a.Id).ToArray());

            var specifications = await _unitOfWork.ProductSpecRepository.GetBySpecAsync(spec);

            var specDtos = _mapper.Map<IEnumerable<ProductSpecDto>>(specifications);

            return Ok(specDtos.GroupBy(a => a.Key).Select(a => new { Name = a.Key, Values = a.Select(v => v.Value).Distinct() }));
        }

        [HttpGet("specifications")]
        public async Task<IActionResult> Specifications() => await Specifications(null);

        [HttpGet("{catalogId?}/hasProducts")]
        public async Task<bool> HasProducts([FromRoute] int? catalogId)
        {
            var catalogs = await GetChildsWithSelfRecursive(catalogId);
            if (catalogs.IsNullOrEmpty())
            {
                return false;
            }

            var product = await _unitOfWork.ProductRepository
                .FindFirstAsync(new ProductByCatalogIdSpec(catalogs.Select(a => a.Id).ToArray()));

            return product != null;
        }

        [HttpGet("hasProducts")]
        public async Task<bool> HasProducts() => await HasProducts(null);

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int? page = null,
            [FromQuery] string sortTitle = null,
            [FromQuery] string search = null,
            [FromQuery] int pageSize = 10)
        {

            if (page.HasValue)
            {
                var spec = new CatalogFilterSpec();
                spec.Query.Paginate((page.Value - 1) * pageSize, pageSize);

                if (sortTitle != null)
                {
                    var orderBy = sortTitle.Equals("desc", StringComparison.OrdinalIgnoreCase) ? OrderBy.DESC : OrderBy.ASC;
                    if (orderBy == OrderBy.ASC) { spec.Query.OrderBy(a => a.Title); }
                    else if (orderBy == OrderBy.DESC) { spec.Query.OrderByDescending(a => a.Title); }
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    spec.Query.Where(catalog => catalog.Title.Contains(search));
                }

                var items = _mapper.Map<IEnumerable<CatalogDto>>(await _unitOfWork.CatalogRepository.GetBySpecAsync(spec));

                var totalCount = string.IsNullOrWhiteSpace(search)
                  ? await _unitOfWork.CatalogRepository.GetAllCountAsync()
                  : items.Count();

                var paginatedList = new Page<CatalogDto>(items, totalCount, page.Value, pageSize);

                return Ok(paginatedList);
            }

            return Ok(_mapper.Map<IEnumerable<CatalogDto>>(
                await _unitOfWork.CatalogRepository.GetBySpecAsync(new CatalogOnlyRootsSpec())));
        }

        [HttpGet("{catalogId}/parents")]
        public async Task<IEnumerable<CatalogDto>> Parents([FromRoute] int catalogId, [FromQuery] bool includeSelf = false)
        {
            var parents = (await GetWithParentsRecursive(catalogId));
            if (!includeSelf)
            {
                parents = parents?.Skip(1)?.ToList();
            }
            return _mapper.Map<IEnumerable<CatalogDto>>(parents);
        }

        [HttpGet("{catalogId?}/childs")]
        public async Task<IEnumerable<CatalogDto>> Childs([FromRoute] int? catalogId, [FromQuery] int? depth = null)
        {
            return await _catalogService.GetChildsRecursive(catalogId, depth);
        }

        [HttpGet("childs")]
        public async Task<IEnumerable<CatalogDto>> Childs([FromQuery] int? depth = null)
            => await Childs(null, depth);

        [Authorize(Roles = AppRoles.Admin)]
        [HttpPost]
        public async Task<IActionResult> Create([FromForm(Name = "catalog")] CatalogDto catalogDto)
        {
            try
            {
                var formFile = Request.Form.Files.FirstOrDefault(a => a.Name == "image");

                var fileData = _mapper.Map<FileData>(formFile);
                await _catalogService.CreateCatalogAsync(catalogDto, fileData);

                return CreatedAtAction(nameof(GetById), new { catalogId = catalogDto.Id }, catalogDto);
            }
            catch
            {
                return BadRequest();
            }
        }

        [Authorize(Roles = AppRoles.Admin)]
        [HttpPut]
        public async Task<IActionResult> Update([FromForm(Name = "catalog")] CatalogDto catalogDto)
        {
            try
            {
                var formFile = Request.Form.Files.FirstOrDefault(a => a.Name == "image");
                var fileData = _mapper.Map<FileData>(formFile);
                await _catalogService.UpdateCatalogAsync(catalogDto, fileData);
                return Ok();
            }
            catch (NotFoundException ex1)
            {
                return NotFound(ex1.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.ToString());
            }
        }

        [Authorize(Roles = AppRoles.Admin)]
        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery(Name = "catalogId")] int[] catalogIds)
        {
            try
            {
                await _catalogService.DeleteCatalogsAsync(catalogIds);
                return Ok();
            }
            catch (NotFoundException ex1)
            {
                return NotFound(ex1.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.ToString());
            }
        }

        [HttpGet("{catalogId}/image")]
        public async Task<ActionResult<CatalogImageDto>> GetImage(int catalogId)
        {
            try
            {
                return await _catalogService.GetCatalogImageAsync(catalogId);
            }
            catch (NotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch
            {
                return BadRequest();
            }
        }

        private async Task<List<Catalog>> GetWithParentsRecursive(int catalogId)
        {
            var catalog = await _unitOfWork.CatalogRepository.FindByIdAsync(catalogId);
            if (catalog is null)
            {
                return null;
            }
            var parents = new List<Catalog>(new[] { catalog });
            if (catalog.ParentCatalogId.HasValue)
            {
                parents.AddRange(await GetWithParentsRecursive(catalog.ParentCatalogId.Value));
            }
            return parents;
        }

        private async Task<List<CatalogDto>> GetChildsWithSelfRecursive(int? catalogId)
        {
            var catalog = _mapper.Map<CatalogDto>(await _unitOfWork.CatalogRepository.FindByIdAsync(catalogId));

            var list = new List<CatalogDto>();

            if (catalog != null)
            {
                list.Add(catalog);
            }

            var childs = await _catalogService.GetChildsRecursive(catalogId);
            if (childs != null && childs.Any())
            {
                list.AddRange(childs);
            }

            return list;
        }
    }
}
