
$version: "2"

namespace com.amazonaws.guidance.productsubs


service Substitutions {
    version: "2023-11-07"
    resources: [Product]
}


resource Product {
    identifiers: { productId: ProductId }

    read: GetProduct

    list: ListProducts
    resources: [Subs]
}

resource Subs {
    identifiers: { productId: ProductId }
    

    read: GetSubs,
}

// "pattern" is a trait.
@pattern("^[A-Za-z0-9 ]+$")
string ProductId

@readonly
    @http(code: 200, method: "GET", uri: "/products/{productId}")
operation GetProduct {
    input: GetProductInput
    output: GetProductOutput
    errors: [NoSuchResource]
}
@input
structure GetProductInput {
    // "productId" provides the identifier for the resource and
    // has to be marked as required.
    @required
    @httpLabel
    productId: ProductId
}
@output
structure GetProductOutput {
    // "required" is used on output to indicate if the service
    // will always provide a value for the member.
    @required
    id: String
}
// "error" is a trait that is used to specialize
// a structure as an error.
@error("client")
structure NoSuchResource {
    @required
    resourceType: String
}
@readonly
    @http(code: 200, method: "GET", uri: "/products")
operation ListProducts {
    input: ListProductsInput
    output: ListProductsOutput
}
@input
structure ListProductsInput {
  
    @httpQuery("nextToken")
    nextToken: String
    @httpQuery("pageSize")
    pageSize: Integer
}
@output
structure ListProductsOutput {
  
    nextToken: String
//    @required
//     items: ProductSummaries
}
// ProductSummaries is a list of ProductSummary structures.
list ProductSummaries {
    member: ProductSummary
}
// ProductSummary contains a reference to a Product.
@references([{resource: Product}])
structure ProductSummary {
    @required
    productId: ProductId
    @required
    name: String
}
@readonly
    @http(code: 200, method: "GET", uri: "/products/{productId}/subs")
operation GetSubs {
    input: GetSubsInput
    output: GetSubsOutput
}
// "productId" provides the only identifier for the resource since
// a Substitution doesn't have its own.
@input
structure GetSubsInput {
    @required
    @httpLabel
    productId: ProductId
}
@output
structure GetSubsOutput {
    productSubs: Integer
}




