﻿using AutoMapper;
using Floward.Product.Catalog.Service.Domain.Dtos;
using Floward.Product.Catalog.Service.Domain.Entities;
using Floward.Product.Catalog.Service.Domain.RabbitMQ.Interfaces;
using Floward.Product.Catalog.Service.Domain.Repos.Interfaces;
using Floward.Product.Catalog.Service.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Floward.Product.Catalog.Service.Services.Implementation
{
    public class ProductService : IProductService
    {
        private readonly IProductRepo _productRepo;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IRabbitManager _manager;

        public ProductService(IProductRepo productRepo, IMapper mapper, IWebHostEnvironment webHostEnvironment, IRabbitManager manager)
        {
            _productRepo = productRepo;
            _mapper = mapper;
            _webHostEnvironment = webHostEnvironment;
            _manager = manager;
        }

        public async Task<ActionResult<IEnumerable<ProductReadDto>>> GetAllProducts()
        {
            var products = await _productRepo.GetAllProducts();
            return new OkObjectResult(_mapper.Map<IEnumerable<ProductReadDto>>(products));
        }

        public async Task<ActionResult<ProductReadDto>> GetProductById(int id)
        {
            var product = await _productRepo.GetProductById(id);
            if (product != null)
            {
                return new OkObjectResult(_mapper.Map<ProductReadDto>(product));
            }

            return new NotFoundResult();
        }

        public async Task<ActionResult<ProductReadDto>> CreateProduct(ProductCreateDto productCreateDto)
        {
            var product = CreateNewProductEntity(productCreateDto);
            var createdProduct = await _productRepo.CreateProduct(product);
            if (createdProduct != null)
            {
                // publish message  
                _manager.Publish(product.Name, "product.exchange", "topic", "product.queue.*");

                return new CreatedResult("/api/Products/" + createdProduct.Id, _mapper.Map<ProductReadDto>(createdProduct));
            }

            // there was error while saving changes
            return new StatusCodeResult(500);
        }

        public async Task<ActionResult<ProductReadDto>> UpdateProduct(int id, ProductCreateDto productCreateDto)
        {
            var product = await _productRepo.GetProductById(id);
            if (product != null)
            {
                product.Name = productCreateDto.Name;
                product.Cost = productCreateDto.Cost;
                product.Price = productCreateDto.Price;
                product.Image = UploadImage(productCreateDto.Image);

                product = await _productRepo.UpdateProduct(product);
                if (product != null)
                {
                    return new OkObjectResult(_mapper.Map<ProductReadDto>(product));
                }

                // there was error while saving changes
                return new StatusCodeResult(500);
            }

            return new NotFoundResult();
        }

        public async Task<ActionResult> DeleteProduct(int id)
        {
            var product = await _productRepo.GetProductById(id);
            if (product != null)
            {
                var isDeleted = await _productRepo.DeleteProduct(product);
                if (isDeleted)
                {
                    return new OkResult();
                }

                // there was error while saving changes
                return new StatusCodeResult(500);
            }

            return new NotFoundResult();
        }


        #region Helper Method
        private string UploadImage(IFormFile image)
        {
            var directoryPath = Path.Combine(_webHostEnvironment.ContentRootPath, "Images");
            var imagePath = Path.Combine(directoryPath, image.FileName);

            using (var stream = new FileStream(imagePath, FileMode.Create))
            {
                image.CopyTo(stream);
            }

            return "/Images/" + image.FileName;
        }

        private ProductEntity CreateNewProductEntity(ProductCreateDto productCreateDto)
        {
            return new ProductEntity()
                {
                    Name = productCreateDto.Name,
                    Cost = productCreateDto.Cost,
                    Price = productCreateDto.Price,
                    Image = UploadImage(productCreateDto.Image)
                };
        }

        #endregion
    }
}
