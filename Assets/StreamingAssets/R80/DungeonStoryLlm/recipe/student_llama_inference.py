# Reachable R80 inference definitions. See recipe-provenance.json.
from public_schema_decoding import decoder_schema
from audit_sft_token_lengths import require

def llama_schema(original):
    """Same bounded language, using llama.cpp's Draft7 positional tuple form.

    b10331 checks `items` before `prefixItems`, so passing the latter together
    with items=false would miscompile it. Unique arrays remain explicit enums.
    """

    def visit(node):
        if type(node) is bool:
            return node
        result = {key: visit(value) if isinstance(value, dict) else value for key, value in node.items()}
        if 'properties' in node:
            result['properties'] = {key: visit(value) for key, value in node['properties'].items()}
        if 'prefixItems' in node:
            require(node.get('items') is False, 'closed public tuple required')
            result['items'] = [visit(item) for item in node['prefixItems']]
            result['additionalItems'] = False
            del result['prefixItems']
        return result
    return visit(decoder_schema(original))

def validated_response(body):
    require(isinstance(body.get('content'), str) and type(body.get('truncated')) is bool, 'missing raw llama completion fields')
    require(body.get('stop') is True and body.get('stop_type') == 'eos' and (body['truncated'] is False), 'completion did not finish at EOS without truncation')
    require(isinstance(body.get('tokens'), list) and body['tokens'], 'raw generated token IDs missing')
    return body['content']
